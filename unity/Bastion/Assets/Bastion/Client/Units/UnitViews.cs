using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>
        /// Moving crouched (hunkered, held, hidden, the squad unseen): the crouched walk, as fast as the gait it would stand
        /// in (Arturo, 2/11: crouched at the plain pace; gaits.ts CROUCHED had its own 0.71 s a tile), its clip quickened
        /// to keep its feet (ClipSpeed). A dash it runs standing (Rises).
        /// </summary>
        public static Gait Crouched(Gait standing) => new Gait("crouchWalk", standing.seconds);

        /// <summary>Whether one crouched gets up for a move of `ap` actions, runs it as one standing and crouches once there (Arturo, 2/11: a dash).</summary>
        public static bool Rises(int ap) => ap >= 2;

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
            /// <summary>Thrown by La Lata as it went (RunOver): it lies where it came down, whatever the state says.</summary>
            public bool runOver;
            public bool shaded;
            /// <summary>On a storey its building's cutaway hides (Cutaway.Hides), or inside a building not found (Unfound): its renderers off.</summary>
            public bool cut;
            /// <summary>Inside a building the squad has not found (Unfound), worked out on this tile at this Sync.</summary>
            public bool unfound;
            public (int x, int y, int z) unfoundAt;
            public int unfoundSync = -1;
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
            /// <summary>One of the dead: which of its kind it is on the map (its turn at the map's bodies, Dealt; −1 none) and the map's id.</summary>
            public int dealt = -1;
            public string map;
            /// <summary>The weapon in its hands (UnitViews.Arms: a gun, the pistol, a blade), its muzzle in its frame, how it is held at rest.</summary>
            public Transform weapon;
            public Vector3 muzzle;
            public string rest;
            /// <summary>What it carries by where it rides (weapon, sidearm, belt: syntyFigures.ts CARRIES), each's look, muzzle and middle, and the places on the body they ride in.</summary>
            public readonly Dictionary<string, Transform> carried = new Dictionary<string, Transform>();
            public readonly Dictionary<string, WeaponArt> arts = new Dictionary<string, WeaponArt>();
            public readonly Dictionary<string, Vector3> muzzles = new Dictionary<string, Vector3>();
            public readonly Dictionary<string, Vector3> butts = new Dictionary<string, Vector3>();
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
            /// <summary>
            /// How far the gun lies between both hands (1) or rigid in the right one at `grip` (0): off while the left hand is
            /// busy (LeftBusy) and back after it, eased (TwoHandsOff, TwoHandsBack). The 3D review's G-1, 10/11.
            /// </summary>
            public float twoHands = 1;
            /// <summary>How far Support has the left palm onto the handguard (0–1): eased in as it comes within reach (G-1).</summary>
            public float supportShare = 1;
            /// <summary>How far it is aimed (0 at rest, 1 aimed), going there over AimBlend.</summary>
            public float aim;
            public bool aiming;
            /// <summary>How far its weapon is up for any of those, crouched or not (unitViews.ts max(aim, aimFx)): what the step back at point blank goes by.</summary>
            public float up;
            /// <summary>Aimed at a target being picked (unitViews.ts aimAt, 23/10 #3): where, with the blade on guard, with the pistol; null none.</summary>
            public (Vector3 at, bool melee, bool sidearm)? picking;
            /// <summary>Where its gun is aimed (RaiseWeapon's point, or the target being picked); none: straight ahead and level (the watch).</summary>
            public Vector3? aimAt;
            /// <summary>The trunk's last turn bringing the barrel onto aimAt (Converge), and how long it is kept so through a shot's kick.</summary>
            public Quaternion converge = Quaternion.identity, convergeArms = Quaternion.identity;
            public float kickLeft;
            /// <summary>How far a shouldered rifle aimed down bends at the hips and knees for it (Bend, degrees), and about what (across the barrel).</summary>
            public float bend;
            public Vector3 bendAxis;
            /// <summary>How far into the pistol's kick (PistolKick); below 0, none.</summary>
            public float pistolKick = -1;
            /// <summary>The back of what it holds (its butt), in the model's own frame, as `muzzle` the front: where a rifle turns in the shoulder (Swing).</summary>
            public Vector3 butt;
            /// <summary>Where its eyes look in its head's frame (from its skeleton's bind pose, which faces ahead: Gaze); zero until found.</summary>
            public Vector3 gaze;
            /// <summary>How far a rifle is brought into the shoulder (Shoulder: 0–1, eased as the gun comes up; off through a reload).</summary>
            public float shouldered;
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
            /// <summary>Gone into hiding (Unit.Hidden: Hide played or begun), and going down for it now.</summary>
            public bool hidden, hiding;
            /// <summary>Up from its crouch for a dash (Walk, Gaits.Rises): Pose leaves it standing till it is there.</summary>
            public bool risen;
            public bool feeding;
            public bool walking;
            public bool striking;
            /// <summary>The yaw it turns to (degrees; null, none: where it is) and how fast (unitViews.ts pose.yawTarget, turn).</summary>
            public float? yawTo;
            /// <summary>
            /// How fast it turns now (degrees a second: under TurnMost, got up to at TurnRise), and whether its weapon waits
            /// for the turn to be nearly done before it comes up (RaiseWeapon, AimAt). The 3D review's A-03, 10/11.
            /// </summary>
            public float yawRate;
            public bool turning;
            /// <summary>Out of the way of the shot over a shoulder (Veil).</summary>
            public bool veiled;
            /// <summary>Gone into La Lata (Board): fading out, then out of sight for good.</summary>
            public bool boarded;
            /// <summary>A shot's kick to play (Recoil) as its step begins.</summary>
            public bool recoil;
            /// <summary>
            /// Eating (Feed, JoinFeed): whom, at which place round the body (EatPlace), on which side, and whether it is down
            /// there (from then on it keeps to that place as the body settles).
            /// </summary>
            public string feedOn;
            public int feedPlace = -1;
            public float feedSide = 1;
            public bool feedDown;
            /// <summary>Reaching for a body it can't get to (ReachFor): whom, and how far the arms are out (0 to 1).</summary>
            public string reachFor;
            public float reachShare;
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
            /// <summary>Time it wasn't stepped, unseen (Step): stepped by it once seen again.</summary>
            public float missed;
            /// <summary>Which of a crowd's two frames it waits in, far off (Step): every other one made.</summary>
            public bool oddPhase;
            /// <summary>How many bites it has taken (Lunge): letting go after one is for that one only.</summary>
            public int bites;
            /// <summary>
            /// The dead's arms out in front (UnitViews.Dead DeadArms): its upper chest's turn in the root's frame as it was
            /// made (the bind pose 0.10.1's rest is measured in), and how far a lunge raises them (aim) and closes them (grab).
            /// </summary>
            public Quaternion? chestRest;
            public float reachAim, reachGrab;
            /// <summary>
            /// How much of the dead's own pose is laid over its clips (DeadPose, DeadArms): 1 alive, down to 0 over DeathEase as
            /// its death comes in (the 3D review's A-04, 10/11: its arms had dropped from out in front to its sides in a frame).
            /// </summary>
            public float life = 1;
            /// <summary>How far into a search's pose it is (Rummage: 0 none, 1 down and rummaging).</summary>
            public float search;
            /// <summary>What it searches (Rummage's point): where its hands go (SearchPose; the 3D review's A-18, 10/11).</summary>
            public Vector3? searchAt;
            /// <summary>Treating (Treat): the one it treats, stepped in to, or itself (its left hand on its thigh).</summary>
            public View tended;
            public bool treatSelf;
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
        /// <summary>Seconds stepped (the arms' sway as they reach).</summary>
        float clock;

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
        /// The shot's own clip (Mixamo's «Firing Rifle», Recoil) wants it too: without it its gun swung 41° off the line.
        /// </summary>
        const float AimBlade = 41;
        /// <summary>
        /// How far the head is turned at most onto the line of fire (Sight, degrees): bladed over «Rifle Aiming Idle» the eyes
        /// looked 40° off it, to the trunk's side (Arturo's #4).
        /// </summary>
        const float SightMost = 60;

        /// <summary>The yaw's ease for steps, strikes and aims (unitViews.ts TURN: toward its target by 1 − e^(−dt × this)).</summary>
        const float Turn = 14;

        /// <summary>
        /// The 3D review's A-03 (10/11): one turning on the spot no faster than TurnMost (degrees a second; walking,
        /// TurnMostWalking), got up to at TurnRise (degrees a second each second: TurnMost in 0.15 s), so a quarter turn takes
        /// some 0.45 s and a half one 0.7 s, not 0.1 s; and its weapon raised once it is within AimTurnLeft of its target (degrees).
        /// </summary>
        const float TurnMost = 360, TurnMostWalking = 720, TurnRise = 2400, AimTurnLeft = 25;

        /// <summary>
        /// A figure not seen isn't animated (Arturo, 1/11, the performance pass: stepping them is 0.6 to 1.5 ms a frame with
        /// 40 to 66 of them): off the game camera's view (its box this wide, its shadow with it), hidden (the fog's, the
        /// cutaway's) or faded out. Only its body waits (Gameplay, 1/11): where it goes and turns, how far up its gun is,
        /// and every wait of the player's go on by the clock. Seen again, its body is stepped by what it missed, up to so
        /// much (a fall or a getting up seen whole after it, at its end).
        /// </summary>
        static readonly Vector3 SeenBox = new Vector3(6, 2.2f, 6);
        const float MissedMost = 10;
        readonly Plane[] frustum = new Plane[6];
        /// <summary>For a lab: figures not seen left unstepped out of play too (RendimientoLab weighs it).</summary>
        public static bool CullOutOfPlay;
        /// <summary>BASTION_ANIMAR_TODAS=1: every figure stepped in play too, seen or not (to weigh it against).</summary>
        static readonly bool StepAll = Environment.GetEnvironmentVariable("BASTION_ANIMAR_TODAS") == "1";

        /// <summary>
        /// A horde in sight (Arturo, 4/11, the performance pass's hordes): with CrowdFrom figures seen or more, the bodies
        /// of the dead farther than ThinFrom (metres, 10 tiles) from where the camera looks are stepped every other frame,
        /// half of them on each, so long as the frames come at 60 a second or more (a body never under 30). As an unseen
        /// one's, only the body waits (Gameplay, 1/11): its skeleton and pose. Where it is and turns, and all the player
        /// waits for, keep to the clock. Never one reaching or biting, nor one down, nor the living.
        /// </summary>
        /// <summary>(BASTION_ALTERNAN_DESDE=n: from n seen, for a lab to make a small turn's far ones wait.)</summary>
        static readonly int CrowdFrom = int.TryParse(Environment.GetEnvironmentVariable("BASTION_ALTERNAN_DESDE"), out int from) ? from : 24;
        const float ThinFrom = 15;
        /// <summary>BASTION_SIN_ALTERNAR=1, or a lab: every body seen stepped each frame (to weigh it against).</summary>
        public static bool ThinOff = Environment.GetEnvironmentVariable("BASTION_SIN_ALTERNAR") == "1";
        /// <summary>How many were seen the frame before (a crowd or not), and whose turn it is to wait this frame.</summary>
        int seenBefore;
        bool oddFrame;
        int made;
        /// <summary>BASTION_ALTERNAN=1: once every 60 frames, how many are seen and how many wait (for a lab's log).</summary>
        static readonly bool ThinLog = Environment.GetEnvironmentVariable("BASTION_ALTERNAN") == "1";
        int thinLogged;

        void Step(float dt)
        {
            clock += dt;
            var cutaway = Map.Cutaway.Current;
            // (Out of play, a lab's: every figure stepped, as its strips have them.)
            var eye = (Application.isPlaying && !StepAll) || CullOutOfPlay ? Camera.main : null;
            if (eye != null) GeometryUtility.CalculateFrustumPlanes(eye, frustum);
            float step = dt;
            oddFrame = !oddFrame;
            bool thin = eye != null && !ThinOff && seenBefore >= CrowdFrom && dt <= 1 / 60f + 1e-4f;
            var look = thin ? Looked(eye) : default;
            int seenNow = 0, waited = 0;
            foreach (var v in views.Values)
            {
                dt = step;
                if (v.yawTo is float yawTo && v.root != null)
                {
                    float yaw = v.root.eulerAngles.y, left = Mathf.DeltaAngle(yaw, yawTo);
                    if (Mathf.Abs(left) < 0.2f) v.yawTo = null;
                    // (As fast as the ease asks, `turn`, as before at its end; but got up to over TurnRise and no faster than
                    // TurnMost: a quarter turn had gone three quarters of the way in its first tenth of a second. Walking, as
                    // the walk's corners ask, at once.)
                    v.yawRate = Mathf.Min(Mathf.Abs(left) * v.turn, Mathf.Min(v.walking ? float.MaxValue : v.yawRate + TurnRise * dt, v.walking ? TurnMostWalking : TurnMost));
                    v.root.rotation = Quaternion.Euler(0, v.yawTo == null ? yawTo : yaw + Mathf.Sign(left) * Mathf.Min(Mathf.Abs(left), v.yawRate * dt), 0);
                    if (v.yawTo == null) v.yawRate = 0;
                    // The weapon up once the turn is nearly done (RaiseWeapon, AimAt).
                    if (v.turning && (v.yawTo == null || Mathf.Abs(left) < AimTurnLeft))
                    {
                        v.turning = false;
                        Settle(v, AimBlend);
                    }
                }
                else if (v.turning)
                {
                    v.turning = false;
                    Settle(v, AimBlend);
                }
                // What stands on a storey the cutaway hides isn't drawn (buildings.ts Cutaway.hides), nor one of the enemy
                // inside a building not found (Unfound; only those drawn worked out).
                StepFade(v, dt);
                if (v.boarded && v.fade <= 0 && v.root.gameObject.activeSelf) v.root.gameObject.SetActive(false);
                bool cut = (cutaway != null && v.place != null && cutaway.Hides(v.place)) || (v.root.gameObject.activeSelf && Unfound(v));
                bool gone = v.fade <= 0;
                Cut(v, cut, gone);
                // Where it is and how far into what it does go on seen or not (Gameplay, 1/11: what the player waits for keeps to the clock).
                bool up = Up(v) && !v.dead;
                v.aim = Mathf.MoveTowards(v.aim, up && !v.crouched ? 1 : 0, dt / AimBlend);
                v.up = Mathf.MoveTowards(v.up, up ? 1 : 0, dt / AimBlend);
                v.reachShare = Mathf.MoveTowards(v.reachShare, v.reachFor != null && !v.dead ? 1 : 0, dt / ReachBlend);
                v.life = Mathf.MoveTowards(v.life, v.dead ? 0 : 1, dt / DeathEase);
                KeepToBody(v, dt);
                bool seen = eye == null || (v.root.gameObject.activeInHierarchy && !cut && !gone && GeometryUtility.TestPlanesAABB(frustum, new Bounds(v.root.position + Vector3.up * SeenBox.y / 2, SeenBox)));
                if (!seen)
                {
                    v.missed = Mathf.Min(v.missed + dt, MissedMost);
                    v.recoil = false;
                    LogFigureNow(v, false);
                    continue;
                }
                seenNow++;
                if (thin && v.deadKind && !v.dead && v.reachFor == null && v.oddPhase == oddFrame
                    && new Vector2(v.root.position.x - look.x, v.root.position.z - look.z).sqrMagnitude > ThinFrom * ThinFrom)
                {
                    v.missed = Mathf.Min(v.missed + dt, MissedMost);
                    waited++;
                    continue;
                }
                // Only the body is left behind: its skeleton and what is posed on it.
                dt += v.missed;
                v.missed = 0;
                StepBack(v, dt);
                if (v.recoil)
                {
                    v.recoil = false;
                    if (v.inHand == Held.Pistol)
                    {
                        // (Converge goes on through it: the kick shows at the wrist, HoldPistol's flip; kept as it was, the turn
                        // for «Pistol Idle 2»'s arms took «Shooting»'s down to the waist.)
                        v.pistolKick = 0;
                    }
                    else
                    {
                        v.anim?.PlayUpper(FireClip, 0.03f);
                        v.kickLeft = FireClip != null ? FireClip.length : 0;
                    }
                }
                PistolKick(v, dt);
                v.anim?.Step(dt);
                if (v.aim > 0 && v.inHand == Held.Rifle)
                {
                    Blade(v, AimBlade * v.aim);
                    Straighten(v, AimStraighten * v.aim);
                    Sight(v, v.aim);
                }
                else if (v.aim > 0 && v.inHand == Held.Pistol)
                {
                    Square(v, v.aim, false);
                    Sight(v, v.aim);
                }
                else if (v.up > 0 && v.crouched && (v.inHand == Held.Rifle || v.inHand == Held.Pistol) && !v.dead)
                {
                    Square(v, v.up, v.inHand == Held.Rifle);
                    Sight(v, v.up);
                }
                HoldNow(v, dt);
                Shoulder(v, dt);
                Converge(v, dt);
                Cheek(v);
                Look(v);
                Support(v, dt);
                SearchPose(v);
                ClimbPose(v);
                Reach(v);
                if (v.deadKind) DeadPose(v);
                if (v.reachShare > 0 && v.reachFor != null && views.TryGetValue(v.reachFor, out var reached)) ReachPose(v, BodyChest(reached), v.reachShare, clock);
                LogFigureNow(v, true);
            }
            seenBefore = seenNow;
            if (ThinLog && ++thinLogged % 60 == 0) Debug.Log($"[alternan] {Time.time:0.0} s: {seenNow} seen, {waited} waiting this frame (crowd {(thin ? "yes" : "no")}, dt {step * 1000:0.0} ms)");
            // (The frame's own step: dt is the last figure's, with what it missed.)
            StepFlashes(step);
            StepRims();
            StepMasks();
            StepMarks(step);
        }

        /// <summary>Where the camera looks on the ground the figures stand on (its line of sight's foot).</summary>
        Vector3 Looked(Camera eye)
        {
            var from = eye.transform.position;
            var along = eye.transform.forward;
            float ground = transform.position.y;
            return Mathf.Abs(along.y) > 1e-4f ? from + along * Mathf.Max(0, (ground - from.y) / along.y) : from;
        }

        /// <summary>
        /// BASTION_LOG_FIGURA=&lt;id&gt; (the labs; the 3D review of 10/11, A-22, A-05, A-14, A-04): that figure each frame as it
        /// is drawn, a line [figura]: what it holds, whether it aims and how far its weapon is up, how far it moves, how
        /// much the whole body's clip and the free hand's take, its fade, its yaw; its head over its feet, each arm's angle
        /// from the trunk's line down and the left palm in its root's frame (cm); where its weapon and eyes point (AimReport,
        /// at its aim or ahead). Unseen (not stepped), the same as it was left.
        /// </summary>
        static readonly string LogFigure = Environment.GetEnvironmentVariable("BASTION_LOG_FIGURA");

        void LogFigureNow(View v, bool seen)
        {
            if (LogFigure == null || v.id != LogFigure || v.root == null) return;
            string F(float f) => f.ToString("0.00", CultureInfo.InvariantCulture);
            string pose = "";
            var a = v.animator;
            if (a != null && a.isHuman && a.GetBoneTransform(HumanBodyBones.Head) is Transform head && a.GetBoneTransform(HumanBodyBones.Hips) is Transform hips
                && a.GetBoneTransform(HumanBodyBones.Neck) is Transform neck)
            {
                var down = hips.position - neck.position;
                string Arm(HumanBodyBones upper, HumanBodyBones hand) => a.GetBoneTransform(upper) is Transform u && a.GetBoneTransform(hand) is Transform h ? F(Vector3.Angle(h.position - u.position, down)) : "-";
                var left = a.GetBoneTransform(HumanBodyBones.LeftHand) is Transform lh ? v.root.InverseTransformPoint(lh.position) * 100 : Vector3.zero;
                pose = $" head={F((head.position.y - v.root.position.y) * 100)} armL={Arm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand)} armR={Arm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand)}"
                    + $" L=({F(left.x)},{F(left.y)},{F(left.z)})";
            }
            var ahead = v.root.position + v.root.forward * 6 + Vector3.up * 1.2f;
            Debug.Log($"[figura] {Time.time.ToString("0.000", CultureInfo.InvariantCulture)} {v.id} seen={seen} held={v.inHand} aiming={v.aiming} up={F(v.up)} picking={v.picking != null} aims={v.aims} moving={F(v.anim?.Moving ?? 0)}"
                + $" whole={F(v.anim?.WholeShare ?? 0)} free={F(v.anim?.FreeShare ?? 0)} fade={F(v.fade)} gone={v.gone} life={F(v.life)} hands={F(v.twoHands)} yaw={F(v.root.eulerAngles.y)}{pose} {AimReport(v.id, v.aimAt ?? ahead)}");
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

        /// <summary>
        /// The trunk turned about the upright, half at the spine and half at the chest, till what it holds points along the
        /// line of fire, `share` of the way, no further than SightMost: the pistol along the right forearm (HoldPistol), a
        /// rifle knelt from the right hand to the left (`hands`: Hold). Mixamo's «Pistol Idle 2» held it some 48° off the
        /// line and «Idle Crouching Aiming» 52°, their hips' turn left out by the upper body's mask (Arturo, 31/10, his #4).
        /// </summary>
        static void Square(View v, float share, bool hands)
        {
            var a = v.animator;
            if (a == null || !a.isHuman || SightOff) return;
            Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand), from = a.GetBoneTransform(hands ? HumanBodyBones.LeftHand : HumanBodyBones.RightLowerArm);
            if (hand == null || from == null) return;
            var arm = hands ? from.position - hand.position : hand.position - from.position;
            var line = v.root.forward;
            arm.y = line.y = 0;
            if (arm.sqrMagnitude < 1e-8f || line.sqrMagnitude < 1e-8f) return;
            Blade(v, Mathf.Clamp(Vector3.SignedAngle(arm, line, Vector3.up), -SightMost, SightMost) * share);
        }

        /// <summary>For the labs: the head and the pistol's trunk as the clips and the blade leave them (Sight and Square off), to set beside it.</summary>
        public static bool SightOff;

        /// <summary>Mixamo's «Rifle fire» (Recoil's), once loaded.</summary>
        static AnimationClip FireClip => fireClip != null ? fireClip : fireClip = FigureCatalog.Load()?.Clip("rifleFire");
        static AnimationClip fireClip;

        /// <summary>
        /// The head turned about the upright onto the line of fire (its root's forward), `share` of the way, half at the neck
        /// and half at the head, no further than SightMost: where its eyes look, as the clips and the blade leave them.
        /// </summary>
        static void Sight(View v, float share)
        {
            var a = v.animator;
            if (a == null || !a.isHuman || SightOff) return;
            Transform left = a.GetBoneTransform(HumanBodyBones.LeftEye), right = a.GetBoneTransform(HumanBodyBones.RightEye);
            Transform neck = a.GetBoneTransform(HumanBodyBones.Neck), head = a.GetBoneTransform(HumanBodyBones.Head);
            if (left == null || right == null || head == null) return;
            var eyes = Vector3.Cross(right.position - left.position, Vector3.up);
            var line = v.root.forward;
            eyes.y = line.y = 0;
            if (eyes.sqrMagnitude < 1e-8f || line.sqrMagnitude < 1e-8f) return;
            float off = Mathf.Clamp(Vector3.SignedAngle(eyes, line, Vector3.up), -SightMost, SightMost) * share;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(off / 2, Vector3.up) * neck.rotation;
            head.rotation = Quaternion.AngleAxis(neck != null ? off / 2 : off, Vector3.up) * head.rotation;
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

        /// <summary>
        /// The barrel onto what it aims at (the review of 9/11, Arturo: «si apuntan bien y el arma y el personaje apuntan a
        /// donde deberían»): after the clips and the trunk's turns (Blade, Straighten, Square) a rifle ran beside the body's
        /// line, not onto the target (5° off at four tiles standing, 11° knelt), some 13° over it shouldered, and never up or
        /// down to another storey (21° under a target a storey up, 47° over one below; CombatLab.Punteria); a pistol the
        /// same. The rest of the way, as far as the gun is up (`up`), twice over:
        /// - a rifle swung round its butt in the shoulder by both arms (Swing), as far as ArmYaw and ArmPitch, and the trunk
        ///   turned only for what is left beyond (a target on another storey): turned at the trunk, the shouldered rifle came
        ///   down to the chest and the back bowed over it (Gameplay, 9/11);
        /// - a pistol by the trunk, half at the spine and half at the chest (its arms are out before it).
        /// Without a point (the watch), straight ahead and level. Through a shot's kick the turns are kept as they were, so
        /// the kick shows.
        /// </summary>
        void Converge(View v, float dt)
        {
            if (v.weapon == null || v.animator == null || !v.animator.isHuman || v.dead || v.reload > 0 || v.striking || v.up <= 0 || SightOff || ConvergeOff
                || (v.inHand != Held.Rifle && v.inHand != Held.Pistol))
            {
                v.converge = v.convergeArms = Quaternion.identity;
                v.kickLeft = 0;
                v.bend = 0;
                return;
            }
            Transform spine = v.animator.GetBoneTransform(HumanBodyBones.Spine), chest = v.animator.GetBoneTransform(HumanBodyBones.Chest);
            if (spine == null || chest == null) return;
            bool rifle = v.inHand == Held.Rifle;
            void Place()
            {
                // (Placed again in the hands, without the pistol's spread from the thigh: that one is stepped once a frame.)
                if (rifle) Hold(v.animator, v.weapon, v.root, false, ref v.grip);
                else HoldPistol(v);
            }
            void Turn(Quaternion q)
            {
                q.ToAngleAxis(out float degrees, out var axis);
                if (degrees > 180) degrees -= 360;
                if (Mathf.Abs(degrees) < 1e-3f) return;
                var half = Quaternion.AngleAxis(degrees / 2, axis);
                spine.rotation = half * spine.rotation;
                chest.rotation = half * chest.rotation;
                Place();
            }
            if (v.kickLeft > 0)
            {
                v.kickLeft -= dt;
                if (rifle && v.bend > 0)
                {
                    Bend(v, v.bend * v.up, v.bendAxis);
                    Place();
                }
                Turn(Quaternion.Slerp(Quaternion.identity, v.converge, v.up));
                if (rifle) Swing(v, Quaternion.Slerp(Quaternion.identity, v.convergeArms, v.up));
                return;
            }
            // What is still off: round the upright and up (degrees), and the axis across the barrel to tip it about.
            (float yaw, float pitch, Vector3 right)? Off()
            {
                var muzzle = v.weapon.TransformPoint(v.muzzle);
                // (The pistol's barrel as the forearm runs, before HoldPistol lays it on the line.)
                Transform hand = v.animator.GetBoneTransform(HumanBodyBones.RightHand), elbow = v.animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                var barrel = !rifle && hand != null && elbow != null ? (hand.position - elbow.position).normalized : v.weapon.forward;
                var want = v.aimAt is Vector3 at ? at - muzzle : new Vector3(v.root.forward.x, 0, v.root.forward.z);
                var flatBarrel = new Vector3(barrel.x, 0, barrel.z);
                var flatWant = new Vector3(want.x, 0, want.z);
                if (flatBarrel.sqrMagnitude < 1e-6f || flatWant.sqrMagnitude < 1e-6f) return null;
                float yaw = Mathf.Clamp(Vector3.SignedAngle(flatBarrel, flatWant, Vector3.up), -ConvergeYaw, ConvergeYaw);
                float pitch = Mathf.Clamp((Mathf.Atan2(want.y, flatWant.magnitude) - Mathf.Atan2(barrel.y, flatBarrel.magnitude)) * Mathf.Rad2Deg, -ConvergePitch, ConvergePitch);
                return (yaw, pitch, Vector3.Cross(Vector3.up, flatBarrel).normalized);
            }
            Quaternion Of(float yaw, float pitch, Vector3 right) => Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, right);
            var trunk = Quaternion.identity;
            var arms = Quaternion.identity;
            if (!rifle)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    if (!(Off() is var (yaw, pitch, right))) break;
                    var q = Of(yaw, pitch, right);
                    trunk = q * trunk;
                    Turn(Quaternion.Slerp(Quaternion.identity, q, v.up));
                }
            }
            else if (Off() is var (yaw0, pitch0, right0))
            {
                // The trunk for what the arms can't swing; then the arms, ArmPasses times (each swing moves the muzzle).
                float trunkYaw = yaw0 - Mathf.Clamp(yaw0, -ArmYaw, ArmYaw), trunkPitch = pitch0 - Mathf.Clamp(pitch0, -ArmPitch, ArmPitch);
                // (Down, HipShare of it at the hips, the knees bent under them: Bend.)
                v.bend = BendOff ? 0 : Mathf.Max(0, -trunkPitch) * HipShare;
                v.bendAxis = right0;
                if (v.bend > 0)
                {
                    Bend(v, v.bend * v.up, right0);
                    Place();
                    trunkPitch += v.bend;
                }
                if (trunkYaw != 0 || trunkPitch != 0)
                {
                    trunk = Of(trunkYaw, trunkPitch, right0);
                    Turn(Quaternion.Slerp(Quaternion.identity, trunk, v.up));
                }
                for (int pass = 0; pass < ArmPasses; pass++)
                {
                    if (!(Off() is var (yaw, pitch, right))) break;
                    var q = Of(Mathf.Clamp(yaw, -ArmYaw, ArmYaw), Mathf.Clamp(pitch, -ArmPitch, ArmPitch), right);
                    arms = q * arms;
                    Swing(v, Quaternion.Slerp(Quaternion.identity, q, v.up));
                }
            }
            v.converge = trunk;
            v.convergeArms = arms;
        }

        /// <summary>How far Converge turns at most: the trunk round the upright and up or down, and a rifle's arms round its butt (degrees).</summary>
        const float ConvergeYaw = 30, ConvergePitch = 60, ArmYaw = 12, ArmPitch = 20;
        static readonly int ArmPasses = (int)LabFloat("BASTION_PASOS_BRAZOS", 3);

        /// <summary>
        /// A shouldered rifle aimed down further than the arms swing it (Arturo, 9/11, in the box: a target a storey below
        /// bowed the back over the gun, its legs straight, some 23° at the trunk): `degrees` of it taken at the hips, about
        /// `axis` (across the barrel), standing the hips let down and back as much as it bends (BendDrop at BendFull), the legs
        /// reaching their feet where they stood, the knees bent the way they were, the feet turned as they were.
        /// </summary>
        void Bend(View v, float degrees, Vector3 axis)
        {
            var a = v.animator;
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null || degrees <= 0) return;
            var legs = new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot) };
            var feet = new (Vector3 at, Quaternion turn, Vector3 knee)[2];
            for (int k = 0; k < 2; k++)
            {
                Transform thigh = a.GetBoneTransform(legs[k].Item1), shin = a.GetBoneTransform(legs[k].Item2), foot = a.GetBoneTransform(legs[k].Item3);
                if (thigh == null || shin == null || foot == null) return;
                // (The knee's way: as the clip has it, ahead where the leg is near straight.)
                var knee = Vector3.ProjectOnPlane(shin.position - thigh.position, foot.position - thigh.position) + v.root.forward * 0.05f;
                feet[k] = (foot.position, foot.rotation, knee);
            }
            float scale = v.body != null ? v.body.lossyScale.x : 1, share = Mathf.Clamp01(degrees / BendFull);
            var back = Vector3.ProjectOnPlane(Vector3.Cross(Vector3.up, axis), Vector3.up).normalized;
            hips.rotation = Quaternion.AngleAxis(degrees, axis) * hips.rotation;
            // (Knelt, only the hips' turn: let down, the knee on the floor went into it.)
            if (!v.crouched) hips.position += (Vector3.down + back * BendBack) * (BendDrop * share * scale);
            for (int k = 0; k < 2; k++)
            {
                var foot = a.GetBoneTransform(legs[k].Item3);
                Reach(a, legs[k].Item1, legs[k].Item2, legs[k].Item3, feet[k].at, feet[k].knee, 0.005f);
                foot.rotation = feet[k].turn;
            }
        }

        /// <summary>
        /// Bend's: how much of the trunk's turn down the hips take, how far they come down at most (metres at a Sidekick's
        /// scale) and back for each metre down, and at how many degrees bent that is reached; BASTION_SIN_RODILLAS=1, none.
        /// </summary>
        static readonly float HipShare = LabFloat("BASTION_CADERA", 0.5f), BendDrop = LabFloat("BASTION_RODILLAS", 0.15f), BendBack = LabFloat("BASTION_ATRAS", 0.4f), BendFull = 12;
        public static bool BendOff = Environment.GetEnvironmentVariable("BASTION_SIN_RODILLAS") == "1";

        /// <summary>
        /// The rifle in the hands turned by `q` round its butt (WeaponArt's model, the back of its box: in the shoulder),
        /// by both arms: each hand where the turn takes it (the arm reaching it, its elbow turned as much) and turned as
        /// much, the head as much (the cheek on the stock); the gun placed again between them (Hold).
        /// </summary>
        void Swing(View v, Quaternion q)
        {
            q.ToAngleAxis(out float degrees, out _);
            if (Mathf.Abs(degrees > 180 ? degrees - 360 : degrees) < 1e-3f) return;
            var a = v.animator;
            var pivot = v.weapon.TransformPoint(v.butt);
            foreach (var (upper, lower, handBone) in new[] { (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand), (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand) })
            {
                Transform shoulder = a.GetBoneTransform(upper), elbow = a.GetBoneTransform(lower), hand = a.GetBoneTransform(handBone);
                if (shoulder == null || elbow == null || hand == null) continue;
                var turned = q * hand.rotation;
                Reach(a, upper, lower, handBone, pivot + q * (hand.position - pivot), q * (elbow.position - shoulder.position), 0.005f);
                hand.rotation = turned;
            }
            // (The head with it, half at the neck and half at the head: the cheek kept on the stock.)
            Transform neck = a.GetBoneTransform(HumanBodyBones.Neck), head = a.GetBoneTransform(HumanBodyBones.Head);
            if (neck != null && head != null)
            {
                q.ToAngleAxis(out float d, out var axis);
                if (d > 180) d -= 360;
                var half = Quaternion.AngleAxis(d / 2, axis);
                neck.rotation = half * neck.rotation;
                head.rotation = half * head.rotation;
            }
            Hold(v.animator, v.weapon, v.root, false, ref v.grip);
        }

        /// <summary>
        /// A rifle brought into the shoulder (Arturo, 9/11, in the box: «la culata en el hueco del hombro, las miras a la
        /// altura del ojo, la mejilla en la culata»): «Rifle Aiming Idle» over Sidekick's shorter arms held it at the chest,
        /// its butt 16 cm under the right shoulder's joint and 26 ahead of it, the right eye 37 cm over the bore
        /// (CombatLab.Punteria, [hombro]). Both hands moved as one, as far as the gun is up, till the butt is in the
        /// shoulder's pocket (Pocket, from the joint in the trunk's frame), the gun turned as it was; Converge then aims it
        /// round there and Cheek brings the face down onto the stock. Through a shot's kick too: the butt stays in the
        /// shoulder and the kick shows at the muzzle (the move kept as it was, the standing kick's clip over a kneel swung
        /// the gun 13° instead of 4–7°).
        /// </summary>
        void Shoulder(View v, float dt)
        {
            bool on = v.inHand == Held.Rifle && v.weapon != null && v.animator != null && v.animator.isHuman && !v.dead && v.reload <= 0 && !v.striking && !ConvergeOff && !ShoulderOff;
            v.shouldered = Mathf.MoveTowards(v.shouldered, on ? 1 : 0, dt / AimBlend);
            float k = v.shouldered * v.up;
            if (k <= 0 || v.inHand != Held.Rifle || v.weapon == null || !(TrunkOf(v) is var (x, y, z, joint))) return;
            float scale = v.body != null ? v.body.lossyScale.x : 1;
            var pocket = joint + (x * Pocket.x + y * Pocket.y + z * Pocket.z) * scale;
            Lift(v, (pocket - v.weapon.TransformPoint(v.butt)) * k);
        }

        /// <summary>
        /// Where the butt goes, from the right shoulder's joint in the trunk's frame (metres at a Sidekick's scale: in, up,
        /// ahead): the pocket between the chest and the shoulder, at the front of it. BASTION_HUECO=x,y,z for the labs.
        /// </summary>
        static readonly Vector3 Pocket = LabVector("BASTION_HUECO", new Vector3(-0.05f, -0.03f, 0.07f));

        /// <summary>BASTION_SIN_HOMBRO=1: the rifle where Converge leaves it, not brought into the shoulder (the labs' «before»).</summary>
        public static bool ShoulderOff = Environment.GetEnvironmentVariable("BASTION_SIN_HOMBRO") == "1";

        /// <summary>A vector from the environment (x,y,z), for the labs to try; `or` without it.</summary>
        static Vector3 LabVector(string name, Vector3 or)
        {
            var parts = (Environment.GetEnvironmentVariable(name) ?? "").Split(',');
            return parts.Length == 3 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ? new Vector3(x, y, z) : or;
        }

        /// <summary>A number from the environment, for the labs to try; `or` without it.</summary>
        static float LabFloat(string name, float or) =>
            float.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : or;

        /// <summary>
        /// The trunk's frame as it stands now: x out to the right (from the left shoulder's joint to the right's), y up the
        /// spine (from the hips to the neck, square to x), z ahead; and the right shoulder's joint. Null without the bones.
        /// </summary>
        static (Vector3 x, Vector3 y, Vector3 z, Vector3 joint)? TrunkOf(View v)
        {
            var a = v.animator;
            Transform rs = a.GetBoneTransform(HumanBodyBones.RightUpperArm), ls = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips), neck = a.GetBoneTransform(HumanBodyBones.Neck);
            if (rs == null || ls == null || hips == null || neck == null) return null;
            var x = (rs.position - ls.position).normalized;
            var y = Vector3.ProjectOnPlane(neck.position - hips.position, x).normalized;
            if (x.sqrMagnitude < 0.5f || y.sqrMagnitude < 0.5f) return null;
            return (x, y, Vector3.Cross(x, y), rs.position);
        }

        /// <summary>The rifle in the hands moved by `d` as it is turned: each hand moved as much (its arm reaching it, the elbow bent the way it was) and kept as it was turned; the gun placed again between them (Hold).</summary>
        void Lift(View v, Vector3 d)
        {
            if (d.sqrMagnitude < 1e-8f) return;
            var a = v.animator;
            foreach (var (upper, lower, handBone) in new[] { (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand), (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand) })
            {
                Transform shoulder = a.GetBoneTransform(upper), elbow = a.GetBoneTransform(lower), hand = a.GetBoneTransform(handBone);
                if (shoulder == null || elbow == null || hand == null) continue;
                var turn = hand.rotation;
                Reach(a, upper, lower, handBone, hand.position + d, elbow.position - shoulder.position, 0.005f);
                hand.rotation = turn;
            }
            Hold(v.animator, v.weapon, v.root, false, ref v.grip);
        }

        /// <summary>
        /// The cheek on the stock (with Shoulder): the face turned at the head onto the barrel's line, CheekBow under it, no
        /// further than CheekTurn; the right eye brought at the neck over the bore at the sights' height (WeaponArt.sight;
        /// CheekOver above it), where it is along the gun, no further than NeckMost; CheekPasses times, each moving the other. As far as
        /// the rifle is shouldered and up. Before: the face 25° to the left of the barrel and 38° under it.
        /// </summary>
        void Cheek(View v)
        {
            float k = v.shouldered * v.up;
            if (k <= 0 || v.inHand != Held.Rifle || v.weapon == null || v.animator == null) return;
            var a = v.animator;
            Transform neck = a.GetBoneTransform(HumanBodyBones.Neck), head = a.GetBoneTransform(HumanBodyBones.Head), eye = a.GetBoneTransform(HumanBodyBones.RightEye);
            if (neck == null || head == null || eye == null) return;
            if (v.gaze == Vector3.zero) v.gaze = Gaze(v, head);
            var w = v.weapon;
            float sight = v.arts.TryGetValue(v.drawn, out var art) && art.sight > 0 ? art.sight : 0.08f;
            Quaternion Most(Quaternion q, float most)
            {
                q.ToAngleAxis(out float degrees, out var axis);
                if (degrees > 180) degrees -= 360;
                if (Mathf.Abs(degrees) < 1e-3f) return Quaternion.identity;
                return Quaternion.AngleAxis(Mathf.Clamp(degrees, -most, most) * k, axis);
            }
            for (int pass = 0; pass < CheekPasses; pass++)
            {
                if (v.gaze != Vector3.zero)
                {
                    var along = w.forward;
                    var right = Vector3.Cross(Vector3.up, along);
                    var want = right.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(CheekBow, right.normalized) * along : along;
                    head.rotation = Most(Quaternion.FromToRotation(head.rotation * v.gaze, want), CheekTurn) * head.rotation;
                }
                var e = w.InverseTransformPoint(eye.position);
                var at = w.TransformPoint(new Vector3(v.muzzle.x, v.muzzle.y + sight + CheekOver / Mathf.Max(w.lossyScale.z, 1e-4f), e.z));
                neck.rotation = Most(Quaternion.FromToRotation(eye.position - neck.position, at - neck.position), NeckMost) * neck.rotation;
            }
        }

        /// <summary>Cheek's: how far under the barrel the face looks and how far the head turns for it, how far the neck bows (degrees), and the eye over the sights (metres).</summary>
        static readonly float CheekBow = LabFloat("BASTION_MEJILLA_BAJA", 8), CheekTurn = LabFloat("BASTION_MEJILLA_GIRO", 50), NeckMost = LabFloat("BASTION_CUELLO", 35), CheekOver = LabFloat("BASTION_MEJILLA_SOBRE", 0.01f);
        static readonly int CheekPasses = (int)LabFloat("BASTION_MEJILLA_PASOS", 3);

        /// <summary>
        /// The face onto what it aims at with the pistol (the review of 9/11; CombatLab.Punteria, along Gaze): the rest of
        /// the way turned at the neck and the head, half each, as far as the gun is up, to LookBow under the line, no
        /// further than LookMost either way. A rifle's head is left as the clip and Sight have it, its cheek on the stock
        /// (Gameplay, 9/11: the face stays over the butt).
        /// </summary>
        void Look(View v)
        {
            if (v.animator == null || !v.animator.isHuman || v.dead || v.reload > 0 || v.striking || v.up <= 0 || SightOff || ConvergeOff || v.inHand != Held.Pistol)
                return;
            Transform le = v.animator.GetBoneTransform(HumanBodyBones.LeftEye), re = v.animator.GetBoneTransform(HumanBodyBones.RightEye);
            Transform neck = v.animator.GetBoneTransform(HumanBodyBones.Neck), head = v.animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null || neck == null) return;
            if (v.gaze == Vector3.zero) v.gaze = Gaze(v, head);
            if (v.gaze == Vector3.zero) return;
            var eyes = le != null && re != null ? (le.position + re.position) / 2 : head.position;
            var look = head.rotation * v.gaze;
            var want = v.aimAt is Vector3 at ? at - eyes : new Vector3(v.root.forward.x, 0, v.root.forward.z);
            var flatLook = new Vector3(look.x, 0, look.z);
            var flatWant = new Vector3(want.x, 0, want.z);
            if (flatLook.sqrMagnitude < 1e-6f || flatWant.sqrMagnitude < 1e-6f) return;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(flatLook, flatWant, Vector3.up), -LookMost, LookMost);
            float pitch = Mathf.Clamp((Mathf.Atan2(want.y, flatWant.magnitude) - Mathf.Atan2(look.y, flatLook.magnitude)) * Mathf.Rad2Deg - LookBow, -LookMost, LookMost);
            var right = Vector3.Cross(Vector3.up, flatLook).normalized;
            var q = Quaternion.Slerp(Quaternion.identity, Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, right), v.up);
            q.ToAngleAxis(out float degrees, out var axis);
            if (degrees > 180) degrees -= 360;
            if (Mathf.Abs(degrees) < 1e-3f) return;
            var half = Quaternion.AngleAxis(degrees / 2, axis);
            neck.rotation = half * neck.rotation;
            head.rotation = half * head.rotation;
        }

        /// <summary>How far under the line to its target the face stays (degrees: a cheek on the stock), and how far Look turns it at most.</summary>
        const float LookBow = 8, LookMost = 45;

        /// <summary>
        /// Where the eyes look in the head's frame: ahead of the body as its skeleton was bound (the bind pose stands
        /// facing the figure's forward, the head level), from the body's skinned mesh; zero without one.
        /// </summary>
        static Vector3 Gaze(View v, Transform head)
        {
            foreach (var r in v.body != null ? v.body.GetComponentsInChildren<SkinnedMeshRenderer>() : Array.Empty<SkinnedMeshRenderer>())
            {
                if (r.sharedMesh == null) continue;
                int k = Array.IndexOf(r.bones, head);
                var binds = r.sharedMesh.bindposes;
                if (k < 0 || k >= binds.Length) continue;
                // (The head's turn at the bind in the world as the mesh stands now; the figure faces its root's forward.)
                var bound = r.transform.rotation * Quaternion.Inverse(binds[k].rotation);
                return Quaternion.Inverse(bound) * v.root.forward;
            }
            return Vector3.zero;
        }

        /// <summary>BASTION_SIN_CONVERGER=1: the barrel as the clips and the trunk's turns leave it, for the labs' «before».</summary>
        public static bool ConvergeOff = Environment.GetEnvironmentVariable("BASTION_SIN_CONVERGER") == "1";

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

        /// <summary>
        /// What the squad sees (Los.TeamVisibility) of `s` as it is now, if the last Sync worked it out for it (that state, at
        /// that Seq), else null: the cutaway goes by it too, not working it out again (1.8 ms on Metsovo's 64 × 64).
        /// </summary>
        public bool[] SightOf(GameState s) => s != null && s == sightOf && s.Seq == sightSeq ? sight : null;

        /// <summary>Sync's parts for the profiler (RendimientoLab.Parones; UI, 7/11: ShowSeen 6 to 35 ms).</summary>
        static readonly Unity.Profiling.ProfilerMarker SyncMarker = new Unity.Profiling.ProfilerMarker("Bastion.Art.Sync"),
            SightMarker = new Unity.Profiling.ProfilerMarker("Bastion.Art.Sync.Sight"), MakeMarker = new Unity.Profiling.ProfilerMarker("Bastion.Art.Sync.Make");

        bool[] sight;
        GameState sightOf;
        int sightSeq;

        /// <summary>The figures as the state seen says: those that come in made, those gone taken away, each on its tile.</summary>
        public void Sync(GameState view)
        {
            using var synced = SyncMarker.Auto();
            state = view;
            // The enemy shows where the squad has it in sight (teamVisibility), or where UI shows it in the fog (a shade). One
            // UI shows as it acts (ForceVisible) is so till now, as 0.10.1's (Ana, 7/11 #14: one of the dead shown once at a
            // contact stayed drawn for good, inside a building not found too).
            bool[] inSight;
            using (SightMarker.Auto()) inSight = Los.TeamVisibility(view, "xcom");
            (sight, sightOf, sightSeq) = (inSight, view, view.Seq);
            syncs++;
            lookers.Clear();
            foreach (var u in view.Units) if (Los.IsViewer(u, Teams.Xcom)) lookers.Add(u.Pos);
            var seen = new HashSet<string>();
            foreach (var u in view.Units)
            {
                if (u.Evacuated) continue;
                seen.Add(u.Id);
                if (!views.TryGetValue(u.Id, out var v))
                    using (MakeMarker.Auto()) views[u.Id] = v = Make(u);
                // (One La Lata threw stays as it came down: alive to the rules, the mission over.)
                if (v.runOver) continue;
                var was = v.place;
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
                // (Who eats a body or reaches for it stays where it is while its tile is the same: put back on the tile's
                // middle at every event, it went on eating a tile off the body, Ana's #7 of 1/11.)
                bool still = was != null && was.X == u.Pos.X && was.Y == u.Pos.Y && was.Z == u.Pos.Z;
                if (!(still && (v.feeding || v.reachFor != null))) v.root.position = MapSpace.StandAt(view, v.place);
                int node = Bastion.Engine.Grid.NodeOf(view, u.Pos);
                // (A body that has got up as one of the dead, Reanimate, is gone: the engine keeps it, Reanimated.)
                bool shown = !u.Alive && u.Reanimated ? false
                    : v.boarded ? v.fade > 0 : u.Team == "xcom" || revealed.Contains(u.Id) || v.shaded || (node >= 0 && node < inSight.Length && inSight[node]);
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
            var view = new View { id = u.Id, template = u.Template, team = u.Team, root = root, seed = u.CampaignId ?? (u.Team == "xcom" ? u.Name : u.Id), armsKey = ArmsKey(u), oddPhase = (made++ & 1) == 1 };
            view.deadKind = Gaits.MovesDead(u.Template);
            if (view.deadKind) (view.dealt, view.map) = (TurnOf(u), state?.MapId);
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
                // kneeling with it; the rest Synty's (with the pistol, its own: 'own'). It stands in its stance (StanceOf).
                string stance = StanceOf(view, u, armed);
                view.stance = stance;
                string ArmedId(string m) => !rifled ? null : m == "crouchIdle" ? "rifleKneel" : Anims.Armed("rifle", m) is string a && a != "own" ? a : null;
                string IdOf(string m) => ArmedId(m) ?? (m == "idle" ? Anims.Stance(view.build, stance) : null) ?? Anims.Motion(view.build, m, view.deadKind);
                if (view.animator != null && catalog != null)
                {
                    // Its bones written whenever its graph is stepped, seen or not (POLYGON's prefabs cull their bones'
                    // updates when unseen, as the Settlement and Portraits found): what Step does here is its own culling. Back
                    // from a veil or a cutaway, the first step's pose had been dropped and the dead's lean laid twice over the
                    // last one, its arms down and its trunk turned for a frame (the 3D review's A-14, 10/11).
                    view.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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
        /// What the state says of how it stands: crouched when hunkered, held or hidden (unitViews.ts crouchTarget; Unit.Hidden,
        /// Esconderse), and the whole squad while it's still unseen (GameState.Concealed; Ana's #1 of 1/11), still and
        /// walking; a soldier slumped under half its health (setStance 'slumped').
        /// </summary>
        void Pose(View v, Unit u)
        {
            if (v.anim == null || v.dead) return;
            bool hidden = u.Hidden == true && u.Alive;
            bool crouched = u.Hunkered || u.Captive || hidden || (u.Team == "xcom" && u.Alive && state != null && state.Concealed);
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
            // Gone into hiding: down behind its cover once (Hide: UI's event player waits for it, else it begins here), not
            // when it was so as the map came in.
            if (!hidden) v.hidden = false;
            else if (!v.hidden)
            {
                v.hidden = true;
                if (v.synced && !v.walking && !v.feeding)
                {
                    _ = GoIntoHiding(v, u);
                    return;
                }
            }
            if (crouched == v.crouched || v.hiding || v.risen) return;
            // Up from the knee where it stands, with a gun in its hands: Mixamo's way up (the review of 9/11, Gameplay).
            if (!crouched && !KneelUpOff && v.synced && !v.walking && !v.feeding && !v.dead && (v.inHand == Held.Rifle || v.inHand == Held.Pistol))
            {
                _ = KneelUp(v);
                return;
            }
            v.crouched = crouched;
            if (!v.walking && !v.feeding) v.anim.Motion(IdleOf(v));
            if (v.weapon != null) Settle(v, 0.3f);
        }

        /// <summary>
        /// Up from the knee to standing with the gun in its hands (the review of 9/11, Gameplay: «levantarse con Rifle/Pistol
        /// Kneel To Stand»; till then a blend of the motions, the legs sliding from the knee to standing): the clip whole,
        /// the standing idle under it, let go as it ends. As GoIntoHiding, it holds `hiding` meanwhile.
        /// </summary>
        /// <summary>BASTION_SIN_LEVANTAR=1: up from the knee as the motions blend, for the labs' «before».</summary>
        static readonly bool KneelUpOff = Environment.GetEnvironmentVariable("BASTION_SIN_LEVANTAR") == "1";

        async Awaitable KneelUp(View v, CancellationToken ct = default)
        {
            v.hiding = true;
            try
            {
                v.crouched = false;
                var clip = FigureCatalog.Load()?.Clip(v.inHand == Held.Rifle ? "rifleKneelUp" : "pistolKneelUp");
                float seconds = clip != null ? v.anim.PlayWholePart(clip, 0, clip.length, 0.15f, false) : 0;
                v.anim.Motion(IdleOf(v));
                if (v.weapon != null) Settle(v, HideFade);
                if (clip == null) return;
                await Awaitable.WaitForSecondsAsync(Mathf.Max(0.3f, seconds - HideFade), ct);
                v.anim.StopWhole(HideFade);
                await Awaitable.WaitForSecondsAsync(HideFade, ct);
            }
            finally
            {
                v.hiding = false;
            }
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
        GameObject FigureOf(Unit u, View view)
        {
            var catalog = FigureCatalog.Load();
            // An objective (the relay: its siren car) is no body: a stand-in until it is drawn as its own.
            if (catalog == null || u.Objective) return null;
            // A soldier of the squad risen as one of the dead: that soldier's figure (Ana's #5, 1/11: the body went and a
            // purple zombie got up).
            if (u.RisenFrom != null && u.Appearance != null && RisenFigureOf(u, view) is GameObject risen) return risen;
            // The special dead: City Zombies' and Boss Zombies' (UnitViews.Dead); Sidekick's dead if their prefab is missing.
            if (DeadFigureOf(u, view, catalog) is GameObject special) return special;
            // One of the common dead in a soldier's uniform (Unit.Outfit; Arturo's A9 of 2/11): City Zombies' soldier.
            if (view.deadKind && u.Outfit == Outfits.Military && DeadFigureOf(Soldiers, view, catalog) is GameObject uniformed) return uniformed;
            // In a patient's gown (who started in a clinic or a hospital; Arturo, 9/11): Horror Asylum's and City Zombies' patients.
            if (view.deadKind && u.Outfit == Outfits.Patient && !NewDeadOff && DeadFigureOf(Patients, view, catalog) is GameObject patient) return patient;
            // In their own clothes, one in CityShare by their id: City Zombies' (Arturo, 9/11: «sueltos en la multitud»).
            if (view.deadKind && u.Template == TemplateIds.Zombie && u.Outfit == null && FromCity(u.Id) && DeadFigureOf(Crowd, view, catalog) is GameObject city) return city;
            string kind = view.deadKind ? "dead" : u.Template;
            if (Bodies.TryGetValue(kind, out var all))
            {
                // (The common dead in a policeman's uniform from Sidekick's police and City Zombies' policewoman, the others
                // never one of them: a deck of their own each, TurnOf.)
                bool cop = view.deadKind && u.Template == TemplateIds.Zombie && u.Outfit == Outfits.Police;
                var bodies = view.deadKind && u.Template == TemplateIds.Zombie ? all.Where(b => IsCop(b.preset) == cop).ToArray() : all;
                if (NewDeadOff) bodies = bodies.Where(b => !UndeadFigure.IsRagged(b.preset)).ToArray();
                if (bodies.Length == 0) bodies = all;
                int dealt = view.deadKind ? Dealt(bodies.Length + (cop && !NewDeadOff ? CrowdCops.Length : 0), view, i => i < bodies.Length ? bodies[i].preset : DealtId(CrowdCops[i - bodies.Length])) : PickIndex(bodies.Length, view.seed);
                if (dealt >= bodies.Length && DeadFigure(CrowdCops[dealt - bodies.Length], view, catalog) is GameObject policewoman) return policewoman;
                var (preset, female) = bodies[Mathf.Min(dealt, bodies.Length - 1)];
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
        /// A soldier risen as one of the dead (Rules.RisenUnit: it keeps their look): the figure they had, their body, face,
        /// hair, clothes and what they wear, only their skin the dead's (Dress.Of risen: SoldierPalette.RisenTone); in their
        /// Personalizar colours, without their player's band (they are nobody's now). Null if it can't be made.
        /// </summary>
        GameObject RisenFigureOf(Unit u, View view)
        {
            // Dressed by the soldier's seed as Make gave it them: their campaign id, or their name («Zombi · Clara Bosch»'s).
            var corpse = state?.Units.FirstOrDefault(x => x.Id == u.RisenFrom);
            int named = u.Name.LastIndexOf(" · ", StringComparison.Ordinal);
            string seed = corpse != null ? corpse.CampaignId ?? corpse.Name : named >= 0 ? u.Name.Substring(named + 3) : u.Name;
            try
            {
                var dress = Dress.Of(u.Appearance, seed, null, true);
                view.build = dress.sex == "f" ? "femn" : "masc";
                var figure = SoldierFigure.Make(dress, "Levantado");
                SquadTint.Paint(figure, u.Appearance, null);
                return figure;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[units] {u.Id}: no risen figure of {u.RisenFrom} ({e.Message})");
                return null;
            }
        }

        /// <summary>
        /// Which of the dead of its kind (the common ones, or each special's) `u` is on the map shown, in the order the state
        /// lists them (which only grows); the risen not counted (they are themselves). −1 if it isn't there (a lab's).
        /// </summary>
        int TurnOf(Unit u)
        {
            if (state == null) return -1;
            string kind = DeckOf(u);
            int n = 0;
            foreach (var x in state.Units)
            {
                if (x.Id == u.Id) return n;
                if (x.RisenFrom == null && Gaits.MovesDead(x.Template) && DeckOf(x) == kind) n++;
            }
            return -1;
        }

        /// <summary>
        /// Which deck one of the dead is dealt from: its special kind's, or the common dead's by what they wore (Unit.Outfit),
        /// in their own clothes City Zombies' or Sidekick's by their id (FromCity).
        /// </summary>
        static string DeckOf(Unit u) => DeadBodies.ContainsKey(u.Template) ? u.Template
            : "dead" + (u.Template == TemplateIds.Zombie ? u.Outfit ?? (FromCity(u.Id) ? "city" : null) : null);

        /// <summary>Whether a body of the common dead's (Bodies' dead) is one of Sidekick's policemen made dead.</summary>
        static bool IsCop(string preset) => preset.StartsWith("skZombieCop");

        /// <summary>
        /// One of the dead's body among `count` (Ana's #6, 1/11: «la misma chica con falda tres veces juntas»): the bodies in
        /// an order of the map's own, dealt out in turn (View.dealt), so that none comes again before all the others have,
        /// and those of a group, listed together, are all different; without a turn, by its seed (0.10.1's pick).
        /// </summary>
        static int Dealt(int count, View view, Func<int, string> idOf)
        {
            if (view.dealt < 0) return PickIndex(count, view.seed);
            var order = Enumerable.Range(0, count).OrderBy(i => Fnv(view.map + "|" + idOf(i))).ThenBy(i => i).ToArray();
            return order[view.dealt % count];
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
        static int PickIndex(int n, string seed) => (int)(Fnv(seed) % (uint)n);

        static uint Fnv(string seed)
        {
            uint h = 0x811c9dc5;
            foreach (char c in seed ?? "") h = unchecked((h ^ c) * 0x01000193);
            return h;
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
            v.life = 0;
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

        /// <summary>Whether `id` has its pistol in hand: its reload is the pistol's (ReloadNow), and so is its sound (Red, 9/11).</summary>
        public bool PistolInHand(string id) => id != null && views.TryGetValue(id, out var v) && v.inHand == Held.Pistol;

        /// <summary>
        /// The shot's kick: Mixamo's fire clip once over the aim, as its next step begins (begun here, the clip's first look
        /// at the body, at once, undid the blade, the head and the grip till the next frame: the gun flicked off the line).
        /// </summary>
        public void Recoil(string id)
        {
            if (views.TryGetValue(id, out var v) && v.weapon != null && (v.inHand == Held.Rifle || v.inHand == Held.Pistol)) v.recoil = true;
        }

        /// <summary>
        /// The pistol's kick (the review of 9/11, Gameplay: «el retroceso con pistolFire»; 0.10.1 had none, its aim held):
        /// its clock, PistolKickSeconds, for HoldPistol's flip at the wrist; and Mixamo's «Shooting» («Firing A Gun») from
        /// PistolKickFrom over the upper body as much as PistolKickShare, in over 0.03 s and out over 0.12: its one shot,
        /// the forearm 4–5° up and back (ClipProbe.Alone BASTION_MANOS=1: 15° to 19° at 0.33 s, 14° by 0.7 s).
        /// </summary>
        static void PistolKick(View v, float dt)
        {
            if (v.pistolKick < 0 || v.anim == null) return;
            var clip = PistolFireClip;
            v.pistolKick += dt;
            if (clip == null || v.pistolKick >= PistolKickSeconds || v.inHand != Held.Pistol || v.dead)
            {
                v.pistolKick = -1;
                v.anim.Scrub(null, 0, 0);
                return;
            }
            float k = v.pistolKick;
            if (PistolKickShare > 0) v.anim.Scrub(clip, PistolKickFrom + k, PistolKickShare * Mathf.Clamp01(Mathf.Min(k / 0.03f, (PistolKickSeconds - k) / 0.12f)));
        }

        /// <summary>
        /// How much of «Shooting» PistolKick lays over the aim: none (9/11). Its own aim is lower than «Pistol Idle 2»'s: all
        /// of it took the pistol down to the waist and back up over 0.4 s; the kick is HoldPistol's flip at the wrist.
        /// </summary>
        const float PistolKickFrom = 0.25f, PistolKickSeconds = 0.45f, PistolKickShare = 0;

        /// <summary>Mixamo's «Shooting» (PistolKick's), once loaded.</summary>
        static AnimationClip PistolFireClip => pistolFireClip != null ? pistolFireClip : pistolFireClip = FigureCatalog.Load()?.Clip("pistolFire");
        static AnimationClip pistolFireClip;

        /// <summary>Seconds one boarding La Lata takes to fade out by its door (Board; Arturo, 31/10, his #1).</summary>
        public const float BoardSeconds = 0.5f;

        /// <summary>
        /// Into La Lata (Arturo, 31/10, his #1: nobody seen climbing in): turned to it, faded out where it stands by its
        /// door over BoardSeconds (dithered, as the veil), then out of sight. UI's event player waits for it.
        /// </summary>
        public void Board(string id)
        {
            if (!views.TryGetValue(id, out var v)) return;
            v.boarded = true;
            if (Map.MapView.Current?.LataAt is Vector3 lata) Face(id, lata);
            v.fadeGoal = 0;
            v.fadeRate = 1 / BoardSeconds;
        }

        /// <summary>
        /// Into hiding (Esconderse, HiddenEvent; Ana's #1 of 1/11): turned to its cover (CoverYaw) and down on a knee with
        /// the rifle or the pistol in hand (Mixamo's «Stand To Kneel»), crouched from there as Pose keeps it while
        /// Unit.Hidden; with neither in hand, or crouched already (the squad unseen), it only turns. UI's event player
        /// waits for it; without that, the Sync that finds it hidden begins it.
        /// </summary>
        public async Awaitable Hide(string id, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || v.dead || v.anim == null) return;
            if (!v.hidden && state?.Units.FirstOrDefault(x => x.Id == id) is Unit u)
            {
                v.hidden = true;
                await GoIntoHiding(v, u, ct);
                return;
            }
            while (v.hiding) await Awaitable.NextFrameAsync(ct);
        }

        /// <summary>Seconds the clip of going down into hiding fades into the crouch over, and the turn to the cover's ease (as Turn).</summary>
        const float HideFade = 0.3f, HideTurn = 6;

        async Awaitable GoIntoHiding(View v, Unit u, CancellationToken ct = default)
        {
            v.hiding = true;
            try
            {
                if (CoverYaw(v, u) is float yaw) TurnTo(v, yaw, HideTurn);
                bool was = v.crouched;
                v.crouched = true;
                string down = was ? null : v.inHand == Held.Rifle ? "rifleKneelDown" : v.inHand == Held.Pistol ? "pistolKneelDown" : null;
                var clip = down != null ? FigureCatalog.Load()?.Clip(down) : null;
                float seconds = clip != null ? v.anim.PlayWholePart(clip, 0, clip.length, 0.15f, false) : 0;
                // (Crouched under it already: the clip lets go into the crouch, the rifle's kneel of the same set.)
                if (!was)
                {
                    v.anim.Motion(IdleOf(v));
                    if (v.weapon != null) Settle(v, HideFade);
                }
                await Awaitable.WaitForSecondsAsync(Mathf.Max(0.45f, seconds - HideFade), ct);
                if (clip == null) return;
                v.anim.StopWhole(HideFade);
                await Awaitable.WaitForSecondsAsync(HideFade, ct);
            }
            finally
            {
                v.hiding = false;
            }
        }

        /// <summary>
        /// The way one hiding on `u`'s tile turns (Hide): to the side of it with cover most toward the nearest of the enemy
        /// in sight, within the angle cover holds against it (Cover.CoverMinDot); with none in sight, to the side with the
        /// most cover, the nearest to where it faces. Null, it stays as it is: no cover, or none against that enemy.
        /// </summary>
        float? CoverYaw(View v, Unit u)
        {
            if (state == null || v.root == null) return null;
            var sides = Cover.CoverSides(state, u.Pos);
            var inSight = Los.TeamVisibility(state, Teams.Xcom);
            Unit near = null;
            foreach (var e in state.Units)
            {
                if (!e.Alive || e.Team != Teams.Alien) continue;
                int node = Bastion.Engine.Grid.NodeOf(state, e.Pos);
                if (node < 0 || node >= inSight.Length || !inSight[node]) continue;
                if (near == null || Bastion.Engine.Grid.Distance(u.Pos, e.Pos) < Bastion.Engine.Grid.Distance(u.Pos, near.Pos)) near = e;
            }
            var toward = near != null ? MapSpace.World(near.Pos.X, near.Pos.Y) - MapSpace.World(u.Pos.X, u.Pos.Y) : v.root.forward;
            toward.y = 0;
            if (toward.sqrMagnitude < 1e-6f) return null;
            toward.Normalize();
            float? best = null;
            float most = float.MinValue;
            for (int k = 0; k < sides.Length; k++)
            {
                if (sides[k] <= 0) continue;
                var d = Bastion.Engine.Grid.Dirs4[k];
                var side = (MapSpace.World(u.Pos.X + d.X, u.Pos.Y + d.Y) - MapSpace.World(u.Pos.X, u.Pos.Y)).normalized;
                float dot = Vector3.Dot(side, toward);
                if (near != null && dot < Cover.CoverMinDot) continue;
                float score = near != null ? dot + sides[k] * 0.1f : sides[k] + dot * 0.5f;
                if (score <= most) continue;
                most = score;
                best = Quaternion.LookRotation(side).eulerAngles.y;
            }
            return best;
        }

        /// <summary>
        /// A shade (heard, not seen): drawn in the fog, dark, nothing of what it is (0.10.1's shade); not inside a building
        /// not found (Unfound).
        /// </summary>
        public void Shade(string id, bool on)
        {
            if (!views.TryGetValue(id, out var v)) return;
            v.shaded = on;
            if (on) v.root.gameObject.SetActive(true);
            if (on) Cut(v, CutNow(v), v.fade <= 0);
        }

        /// <summary>
        /// Shown even out of sight (an enemy UI reveals as it acts: a contact, a step into sight), till the next Sync works
        /// out again who is in sight (0.10.1's forceVisible); not inside a building the squad has not found (Unfound: Ana,
        /// 7/11 #14), where it shows as it steps out.
        /// </summary>
        public void ForceVisible(string id)
        {
            if (!views.TryGetValue(id, out var v)) return;
            // Out of the fog (a step into sight, a contact): faded in, as Sync does (playtest 20/10 #10).
            if (!v.root.gameObject.activeSelf && v.synced && !v.dead) StartFade(v, FadeIn);
            v.root.gameObject.SetActive(true);
            Cut(v, CutNow(v), v.fade <= 0);
        }

        /// <summary>
        /// Whether `id`'s figure is drawn now: shown (Shown), and neither cut away (the cutaway's storey, a building not
        /// found: Unfound) nor faded out. What goes over a figure (a mark, a word) goes by this.
        /// </summary>
        public bool Drawn(string id) => id != null && views.TryGetValue(id, out var v) && v.root != null && v.root.gameObject.activeSelf && !v.cut && !v.gone;

        /// <summary>For a lab: `id` drawn wherever it is put, in sight or not, through every Sync after.</summary>
        public void Reveal(string id)
        {
            revealed.Add(id);
            ForceVisible(id);
        }

        readonly HashSet<string> revealed = new HashSet<string>();

        /// <summary>The squad's places that look (Los.IsViewer), as at the last Sync: what they find counts as found (Unfound).</summary>
        readonly List<Pos> lookers = new List<Pos>();
        int syncs;

        /// <summary>
        /// Whether `v`, one of the enemy, stands where it is drawn now inside a building the squad has not found (the engine's
        /// Buildings.Hidden, as Los.TeamVisibility veils it; who stands in its doorway shows): not drawn there, shown in the
        /// fog or not (Ana, 7/11 #14: «mientras esté en una zona sin descubrir, no se ve»). Worked out again as it steps
        /// onto another tile, or the state seen changes.
        /// </summary>
        bool Unfound(View v)
        {
            if (state == null || v.team == "xcom" || v.root == null || revealed.Contains(v.id)) return false;
            var p = v.root.position;
            var tile = MapSpace.WorldToTile(p);
            var at = (tile.x, tile.y, Mathf.Max(0, Mathf.FloorToInt((p.y + 0.5f) / MapSpace.Storey)));
            if (at == v.unfoundAt && v.unfoundSync == syncs) return v.unfound;
            (v.unfoundAt, v.unfoundSync) = (at, syncs);
            return v.unfound = Buildings.Hidden(state, new Pos { X = at.Item1, Y = at.Item2, Z = at.Item3 }, lookers);
        }

        /// <summary>Whether `v` is cut away now (the cutaway's storey, or a building not found).</summary>
        bool CutNow(View v) => (Map.Cutaway.Current is Map.Cutaway c && v.place != null && c.Hides(v.place)) || Unfound(v);

        /// <summary>Its renderers off while it is cut away (`cut`) or faded out (`gone`), on again once neither.</summary>
        void Cut(View v, bool cut, bool gone)
        {
            if (cut == v.cut && gone == v.gone) return;
            v.cut = cut;
            v.gone = gone;
            foreach (var r in v.root.GetComponentsInChildren<Renderer>(true)) r.enabled = !cut && !gone;
            if (!cut && !gone && v.fade < 1) foreach (var w in v.carried.Values) if (w != null) Show(w, v.fade >= 0.5f);
        }

        /// <summary>
        /// Out of the way of the shot over a shoulder (unitViews.ts clearView, syntyFigures.ts veil): `on`, not drawn; off,
        /// back. UI says who and when each frame the shot is up (Gameplay 26/10). For now at once (0.10.1 faded it, dithered,
        /// over 0.2 s).
        /// </summary>
        public void Veil(string id, bool on)
        {
            if (!views.TryGetValue(id, out var v) || v.veiled == on) return;
            v.veiled = on;
            // Dithered out over VeilSeconds, or back in (syntyFigures.ts veil); one gone into La Lata stays out.
            if (v.boarded) return;
            v.fadeGoal = on ? 0 : 1;
            v.fadeRate = 1 / (on ? VeilSeconds : VeilBack);
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
                if (!v.aiming) v.aimAt = null;
                _ = Lowered(v, ++v.aims);
                return;
            }
            if (was is { } w && w.melee == melee && w.sidearm == sidearm && (w.at - p).sqrMagnitude < 1e-4f) return;
            v.picking = (p, melee, sidearm);
            v.aimAt = melee ? (Vector3?)null : p;
            ++v.aims;
            Face(id, p);
            if (v.weapon == null) return;
            string carry = melee ? BeltCarry : sidearm ? SidearmCarry : MainCarry;
            if (v.drawn != carry) Draw(v, carry);
            v.close = melee ? 0 : Flat(p - v.root.position) / MapSpace.Tile;
            // (Picked with the gun down and well off the line: turned first, then up; another picked after, it turns with
            // the gun up, 23/10 #3. The 3D review's A-03, 10/11.)
            if (was == null && v.up < 0.99f && TurnLeft(v) > AimTurnLeft) v.turning = true;
            Settle(v, 0.3f);
        }

        // ------------------------------------------------------------ to wait for

        /// <summary>The seconds each step of `id`'s walk along `path` from `from` takes (for walks played together).</summary>
        public float[] StepSeconds(string id, Pos from, IReadOnlyList<Pos> path, int ap = 1, float pace = 1)
        {
            string template = views.TryGetValue(id, out var v) ? v.template : "";
            var (one, two) = Gaits.Of(template);
            var gait = ap >= 2 ? two : one;
            if (v != null && v.crouched && !Gaits.Rises(ap)) gait = Gaits.Crouched(gait);
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
        /// <summary>
        /// Seconds a walk slows to a stop over at its end (Walk), of its last stretch at most StopShare, its clip down to
        /// StopLegs of its speed (then the idle). The 3D review's A-06, 10/11.
        /// </summary>
        const float StopSeconds = 0.3f, StopShare = 0.35f, StopLegs = 0.25f;
        /// <summary>BASTION_SIN_FRENAR=1: stopped at once at the end, for the labs' «before».</summary>
        public static bool StopOff = Environment.GetEnvironmentVariable("BASTION_SIN_FRENAR") == "1";

        async Awaitable Walk(string id, IReadOnlyList<Pos> path, int ap, float pace, IReadOnlyList<float> waits, IReadOnlyList<bool> passes, Action<Vector3> onStep,
            CancellationToken ct)
        {
            if (!views.TryGetValue(id, out var v) || path.Count == 0 || state == null) return;
            // One eating gets up first (unitViews.ts moveAlong: unfeed); one reaching lets its arms down as it goes.
            v.reachFor = null;
            if (v.feeding) await GetUp(v, ct);
            var (one, two) = Gaits.Of(v.template);
            var gait = ap >= 2 ? two : one;
            // Crouched, a dash is run standing (Gaits.Rises): it gets up as it sets off, in the motions' blend, and crouches
            // again once there, as the state still has it (Pose), in the same time as one standing.
            bool rises = v.crouched && Gaits.Rises(ap);
            if (v.crouched && !rises) gait = Gaits.Crouched(gait);
            if (rises)
            {
                v.crouched = false;
                v.risen = true;
                if (v.weapon != null) Settle(v, 0.2f);
            }
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
                    v.anim?.Motion(IdleOf(v));
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
                // At the gait's one speed all along (a diagonal as fast as a straight step), facing where it heads; on the
                // last stretch, slowed to a stop over StopSeconds at its end (a third of a short dash's at most), the rest a
                // little quicker, in the same time all told, its clip as quick as its root to keep its feet (the 3D review's
                // A-06, 10/11: from 6 m/s to still in a frame).
                float seconds = way.length / MapSpace.Tile * tile;
                float e = !StopOff && m == path.Count && seconds > 0 ? Mathf.Clamp(StopSeconds / seconds, 0, StopShare) : 0, v0 = 1 / (1 - e / 2);
                for (float t = 0; t < seconds;)
                {
                    await Awaitable.NextFrameAsync(ct);
                    t = Mathf.Min(seconds, t + Time.deltaTime);
                    float u = seconds > 0 ? t / seconds : 1, past = u - (1 - e);
                    float done = e <= 0 ? u : past <= 0 ? v0 * u : v0 * (1 - e) + v0 * past - v0 * past * past / (2 * e);
                    if (e > 0) v.anim?.Motion(gait.motion, clip * Mathf.Max(StopLegs, past <= 0 ? v0 : v0 * (1 - past / e)));
                    float d = seconds > 0 ? Mathf.Min(1, done) * way.length : way.length;
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
            if (v.risen)
            {
                v.risen = false;
                if (state.Units.FirstOrDefault(x => x.Id == id) is Unit u) Pose(v, u);
            }
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
            v.aimAt = at;
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
            // Down and well off the line: turned first, then up (the 3D review's A-03, 10/11: turned and raised in the same
            // frame). Up already (picked, on watch: 23/10 #3), it turns with the gun up.
            if (from < 0.99f && TurnLeft(v) > AimTurnLeft) v.turning = true;
            Settle(v, AimBlend * (1 - from));
            for (float t = 0; v.turning && t < 0.8f; t += Time.deltaTime) await Awaitable.NextFrameAsync(ct);
            if (from < 0.99f) await Awaitable.WaitForSecondsAsync(0.35f * (1 - from), ct);
            return aim;
        }

        /// <summary>How far `v` has still to turn (degrees; 0 none).</summary>
        static float TurnLeft(View v) => v.yawTo is float y && v.root != null ? Mathf.Abs(Mathf.DeltaAngle(v.root.eulerAngles.y, y)) : 0;

        /// <summary>
        /// Down from aiming (unitViews.ts lowerWeapon): `aim` (RaiseWeapon's) only, not one taken since; to the watch's aim
        /// if it is on watch. Its main weapon (the blade, on guard) drawn again once lowered, the step back gone.
        /// </summary>
        public async Awaitable LowerWeapon(string id, int aim, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || aim != v.aims) return;
            v.aiming = false;
            if (v.picking == null) v.aimAt = null;
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
                // (In over its wind-up, not 0.1 s: the head came up 46 cm in a frame at 30 fps; the 3D review's A-05, 10/11.)
                v.anim.PlayUpper(butt, WindUp, 1, HeadbuttLead);
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
            // (One eating or reaching that dies lets go of the body: it falls where it is.)
            v.feeding = false;
            v.feedOn = null;
            v.reachFor = null;
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

        /// <summary>
        /// unitViews.ts FEED_WAIT, FEED_SIDE, FEED_SEEN: before it goes down; how far beside the body's chest it kneels
        /// (tiles); how long it is seen eating. The waits halved (Arturo's #6 of 1/11, «están demasiado rato comiendo»: the
        /// kill about half as long, with its going down quicker, GoDown, and EventPlayer's hold after it).
        /// </summary>
        const float FeedWait = 0.2f, FeedSide = 0.4f, FeedSeen = 0.4f;
        /// <summary>The going down on its knees to eat played this much quicker (#6: its clip's two seconds in about one).</summary>
        const float DownSpeed = 1.8f;
        /// <summary>
        /// The places round a body (Ana's #6, 1/11): 0 by its chest on the killer's side, 1 by its chest on the other, 2 past
        /// its head, 3 by its knees (on the side the one there came from); how far out from the body those who join kneel
        /// (tiles: further than the killer, so three down eating don't pile up on the chest, UI 1/11), how far past the
        /// head (0.8 till the review of 9/11: the one there bit the air half a metre short of it), and how far back from
        /// the hips the knees are.
        /// </summary>
        const int Places = 4;
        const float JoinSide = 0.62f, PastHead = 0.5f, Knees = 0.7f;
        /// <summary>How fast one eating keeps up with the body as it settles (tiles a second), and turns to it.</summary>
        const float KeepUp = 0.8f;
        /// <summary>
        /// Reaching for a body (ReachFor): seconds for the arms to go out or come down; the trunk's lean in (degrees), and how
        /// much of it comes and goes as it strains (twice a second or so), the feet where they are.
        /// </summary>
        const float ReachBlend = 0.35f, ReachLean = 34, ReachStrain = 0.25f;

        /// <summary>
        /// Where one eats `body` at place `k`, on `side` (±1, across the body: the side it came from), and what it faces
        /// there; on the floor at `y`.
        /// </summary>
        (Vector3 at, Vector3 on) EatPlace(View body, int k, float side, float y)
        {
            var chest = BodyChest(body);
            var hips = Bone(body.id, HumanBodyBones.Hips) ?? body.root.position;
            var head = Bone(body.id, HumanBodyBones.Head) ?? chest;
            var along = head - hips;
            along.y = 0;
            along = along.sqrMagnitude < 1e-4f ? body.root.forward : along.normalized;
            var across = new Vector3(along.z, 0, -along.x) * side;
            float t = MapSpace.Tile;
            var knees = hips - along * Knees * t;
            var (at, on) = k switch
            {
                1 => (chest - across * JoinSide * t, chest),
                2 => (head + along * PastHead * t, head),
                3 => (knees + across * JoinSide * t, knees),
                _ => (chest + across * FeedSide * t, chest),
            };
            at.y = y;
            return (at, on);
        }

        /// <summary>Which side of `body` (±1, across it) `from` is on.</summary>
        float SideOf(View body, Vector3 from)
        {
            var hips = Bone(body.id, HumanBodyBones.Hips) ?? body.root.position;
            var head = Bone(body.id, HumanBodyBones.Head) ?? BodyChest(body);
            var along = head - hips;
            var across = new Vector3(along.z, 0, -along.x);
            return Vector3.Dot(across, from - BodyChest(body)) < 0 ? -1 : 1;
        }

        /// <summary>The places round `victim` free now, those taken by others eating it left out.</summary>
        List<int> FreePlaces(string victim)
        {
            var free = Enumerable.Range(0, Places).ToList();
            foreach (var other in views.Values)
                if (other.feedOn == victim) free.Remove(other.feedPlace);
            return free;
        }

        /// <summary>
        /// The killer eats the fallen (0.10.1, 0.9.7: Mixamo's; its bite was its Lunge): a moment on, it goes down on its
        /// knees beside the body at its chest, across the way it lies, on the side nearer where it stood, coming after it as
        /// it falls; and eats, round and round, keeping to its place by the body (KeepToBody), until it gets up at its next
        /// action (StandUpFed, or as it walks). `onHit` as it starts eating. Ends once it has been seen eating a little.
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
            k.feedOn = victim;
            k.feedPlace = 0;
            k.feedDown = false;
            await Awaitable.WaitForSecondsAsync(FeedWait, ct);
            await GoDown(k, body, ct);
            onHit?.Invoke();
            await Awaitable.WaitForSecondsAsync(FeedSeen, ct);
        }

        /// <summary>
        /// Whether `id` can join in eating `victim` (Ana's #6, 1/11): one of the dead that eat, up and not eating, on a tile
        /// beside the body's (the eight round it, on its storey), with a place left by the body.
        /// </summary>
        public bool CanJoinFeed(string id, string victim) =>
            views.TryGetValue(id, out var v) && views.TryGetValue(victim, out var body) && Feeders.Contains(v.template) && !v.dead && !v.feeding
            && v.place != null && body.place != null && v.place.Z == body.place.Z
            && Mathf.Max(Mathf.Abs(v.place.X - body.place.X), Mathf.Abs(v.place.Y - body.place.Y)) <= 1 && FreePlaces(victim).Count > 0;

        /// <summary>
        /// One of the dead beside a body another has started eating joins in (Ana's #6: only on the screen, the rules
        /// unchanged): the free place by the body nearest it is its own at once (so the next one called takes another), and
        /// a beat on it goes down on its knees there and eats as the killer does. Ends once it has been seen eating a little.
        /// </summary>
        public async Awaitable JoinFeed(string id, string victim, CancellationToken ct = default)
        {
            if (!CanJoinFeed(id, victim)) return;
            var v = views[id];
            var body = views[victim];
            v.reachFor = null;
            if (v.home == Vector3.zero) v.home = v.root.position;
            float side = SideOf(body, v.home);
            var free = FreePlaces(victim);
            v.feedPlace = free.OrderBy(k => Vector3.Distance(EatPlace(body, k, k == 3 ? side : FeedSideOf(victim), v.home.y).at, v.home)).First();
            v.feedSide = v.feedPlace == 3 ? side : FeedSideOf(victim);
            v.feedOn = victim;
            v.feeding = true;
            v.feedDown = false;
            fed.Add(id);
            if (v.anim == null || FigureCatalog.Load() == null)
            {
                Face(id, BodyChest(body));
                await Awaitable.WaitForSecondsAsync(1.2f, ct);
                return;
            }
            // (Each a little after the killer, not all at once: by its id.)
            await Awaitable.WaitForSecondsAsync(FeedWait + 0.05f + PickIndex(4, id + "|join") * 0.05f, ct);
            await GoDown(v, body, ct);
            await Awaitable.WaitForSecondsAsync(FeedSeen, ct);
        }

        /// <summary>The side the killer of `victim` eats it from (place 0's), or 1.</summary>
        float FeedSideOf(string victim)
        {
            foreach (var other in views.Values)
                if (other.feedOn == victim && other.feedPlace == 0) return other.feedSide;
            return 1;
        }

        /// <summary>One eating `body` down on its knees at its place, coming after the body as it falls; then eating round and round.</summary>
        async Awaitable GoDown(View v, View body, CancellationToken ct)
        {
            var catalog = FigureCatalog.Load();
            var start = v.root.position;
            if (v.feedPlace == 0) v.feedSide = SideOf(body, v.home);
            float down = 0.6f;
            // (Its way to its place as long as the clip, quickened.)
            if (Anims.Feed("down") is var (downId, downFrom, downTo)) down = Mathf.Max(0.6f, v.anim.PlayWholePart(catalog.Clip(downId), downFrom, downTo, 0.2f, false, DownSpeed));
            for (float t = 0; t < down && v.feeding; t += Time.deltaTime)
            {
                var (at, on) = EatPlace(body, v.feedPlace, v.feedSide, v.home.y);
                v.root.position = Vector3.Lerp(start, at, Mathf.SmoothStep(0, 1, Mathf.Min(1, t / down * 1.3f)));
                Face(v.id, on);
                await Awaitable.NextFrameAsync(ct);
            }
            if (!v.feeding) return;
            v.feedDown = true;
            if (Anims.Feed("loop") is var (loopId, loopFrom, loopTo)) v.anim.PlayWholePart(catalog.Clip(loopId), loopFrom, loopTo, 0.25f, true);
        }

        /// <summary>One down eating kept at its place by the body as the body settles (its fall, its tuck), turned to it.</summary>
        void KeepToBody(View v, float dt)
        {
            if (!v.feeding || !v.feedDown || v.dead || v.feedOn == null || !views.TryGetValue(v.feedOn, out var body)) return;
            var (at, on) = EatPlace(body, v.feedPlace, v.feedSide, v.home.y);
            v.root.position = Vector3.MoveTowards(v.root.position, at, KeepUp * MapSpace.Tile * dt);
            var d = on - v.root.position;
            d.y = 0;
            if (d.sqrMagnitude > 1e-4f) TurnTo(v, Quaternion.LookRotation(d).eulerAngles.y);
        }

        /// <summary>
        /// One of the dead near a body others are eating, that can't get to it (Ana's #6: no place left, or not beside it):
        /// where it stands, turned to it, its arms out towards it and leaning in, till its next action (StandUpFed, or as it
        /// walks). Ends once it has been seen reaching a moment.
        /// </summary>
        public async Awaitable ReachFor(string id, string victim, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || !views.TryGetValue(victim, out var body) || v.dead || v.feeding) return;
            await Awaitable.WaitForSecondsAsync(PickIndex(4, id + "|reach") * 0.05f, ct);
            v.reachFor = victim;
            Face(id, BodyChest(body));
            await Awaitable.WaitForSecondsAsync(FeedSeen, ct);
        }

        /// <summary>
        /// Arms out towards `to` (a body on the ground), `share` of the way, over what the clips have them in: the trunk
        /// leaning in by ReachLean, straining (by `time`); each upper arm and forearm turned to point at it, groping; the
        /// head turned down to it.
        /// </summary>
        static void ReachPose(View v, Vector3 to, float share, float time)
        {
            var a = v.animator;
            if (a == null || !a.isHuman) return;
            var right = v.root.right;
            float lean = ReachLean * (1 - ReachStrain + ReachStrain * Mathf.Sin(time * 4.4f + v.id.GetHashCode() % 7)) * share;
            foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest })
                if (a.GetBoneTransform(bone) is Transform b) b.rotation = Quaternion.AngleAxis(lean / 2, right) * b.rotation;
            var arms = new[] { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) };
            for (int k = 0; k < arms.Length; k++)
            {
                Transform upper = a.GetBoneTransform(arms[k].Item1), lower = a.GetBoneTransform(arms[k].Item2), hand = a.GetBoneTransform(arms[k].Item3);
                if (upper == null || lower == null || hand == null) continue;
                // (Aimed a little above the body and apart, each hand its own way, groping.)
                var aim = to + Vector3.up * 0.15f + right * (k == 0 ? -0.2f : 0.2f) + new Vector3(Mathf.Sin(time * 2.1f + k * 1.9f), Mathf.Sin(time * 2.7f + k), Mathf.Cos(time * 1.7f + k * 2.3f)) * 0.12f;
                var want = aim - upper.position;
                upper.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(lower.position - upper.position, want), share) * upper.rotation;
                lower.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hand.position - lower.position, aim - lower.position), share) * lower.rotation;
            }
            // (The head down to it: from where it looks along the root to the body, half of it.)
            if (a.GetBoneTransform(HumanBodyBones.Head) is Transform head)
            {
                var down = to - head.position;
                var ahead = Vector3.ProjectOnPlane(down, Vector3.up);
                if (ahead.sqrMagnitude > 1e-4f) head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(ahead, down), share * 0.5f) * head.rotation;
            }
        }

        /// <summary>A body's chest where it lies (its bones), or where it stands.</summary>
        Vector3 BodyChest(View v) => Bone(v.id, HumanBodyBones.Chest) ?? v.root.position;

        /// <summary>Those eating get up and those reaching let their arms down (all at once: the dead's turn starts).</summary>
        public async Awaitable StandUpFed(CancellationToken ct = default)
        {
            foreach (var v in views.Values) v.reachFor = null;
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
            v.feedOn = null;
            v.feedPlace = -1;
            v.feedDown = false;
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
        /// 0.10.1's syntyFigures.ts throwMotion, tossStone): the wind-up cut short, the throw whole. The grenade's, «Throw»
        /// («Throwing An Object From A Standard Pose», throwGrenade; the 3D review's A-16, 10/11): «Toss Grenade» was an
        /// underhand toss, its hand back by the hip at most (ClipProbe.Alone BASTION_MANOS=1: 0.73–0.85 m up), and from the
        /// game's camera it read as two arms opened. «Throw»'s left hand is furthest back over the shoulder at 0.55 s (40 cm
        /// behind, 1.48 m up) and lets go over the head at 0.78 s going forward at 6.4 m/s: begun 0.1 s before the hand is
        /// back.
        /// </summary>
        const float GrenadeRelease = 0.78f, GrenadeLead = 0.78f - 0.55f + 0.1f, StoneRelease = 2.4f, StoneLead = 0.24f;

        /// <summary>A stone thrown at `to` (its throw by the free hand: Mixamo's, mirrored); the hand it leaves, for the stone's flight.</summary>
        public async Awaitable<Vector3> TossStone(string id, Vector3 to, CancellationToken ct = default)
        {
            Face(id, to);
            Transform stone = null;
            if (views.TryGetValue(id, out var v))
            {
                await FacedFirst(v, ct);
                v.anim?.PlayFree(FigureCatalog.Load()?.Clip("throwStone"), 0.1f, 1, StoneRelease - StoneLead);
                ThrowTurn(v, StoneTurn);
                stone = HeldStone(v);
            }
            try
            {
                await Awaitable.WaitForSecondsAsync(StoneLead, ct);
            }
            finally
            {
                if (stone != null) DestroyNow(stone.gameObject);
            }
            if (v != null) ThrowTurn(v, -StoneTurn);
            var at = LeftHand(id);
            if (v != null) _ = StoneDone(v, ct);
            return at;
        }

        /// <summary>ThrowDone for the stone, on by itself (TossStone returns as it lets go, for the stone's flight).</summary>
        static async Awaitable StoneDone(View v, CancellationToken ct)
        {
            try
            {
                await ThrowDone(v, ct);
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// A stone in the left palm till it lets it go (the 3D review's A-16, 10/11: it left an open, empty hand): as Fx.Stone's
        /// in the air (StoneSize tiles, its grey), by its middle in the cup of the hand, as the grenade is (ThrowMotion).
        /// </summary>
        static Transform HeldStone(View v)
        {
            if (v.animator == null || !v.animator.isHuman || !(v.animator.GetBoneTransform(HumanBodyBones.LeftHand) is Transform hand)) return null;
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            stone.name = "piedra";
            DestroyNow(stone.GetComponent<Collider>());
            if (stoneMaterial == null)
            {
                stoneMaterial = KitMaterials.New(KitMaterials.Plain, "Piedra en la mano");
                if (stoneMaterial != null) stoneMaterial.color = new Color32(0x77, 0x73, 0x6b, 0xff);
            }
            if (stoneMaterial != null) stone.GetComponent<Renderer>().sharedMaterial = stoneMaterial;
            stone.SetParent(hand, false);
            stone.localScale = StoneSize * MapSpace.Tile / Mathf.Max(hand.lossyScale.x, 1e-4f);
            var along = v.animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) is Transform finger ? finger.position - hand.position : Vector3.zero;
            stone.position = hand.position + along * 0.75f;
            return stone;
        }

        /// <summary>Fx.Stone's size (tiles, along its three axes).</summary>
        static readonly Vector3 StoneSize = new Vector3(0.12f, 0.086f, 0.106f);
        static Material stoneMaterial;

        /// <summary>
        /// How far to its left one throwing turns as it throws, and back once it lets go (degrees; the review of 9/11,
        /// Gameplay): the throws are Mixamo's thrown with the right hand, mirrored to the left, and the left hand let go
        /// going 55° to the right of where the thing lands (the stone 30°; CombatLab.Lanzar), its arm across the body.
        /// «Throw» (the grenade's since the 3D review's A-16, 10/11) throws nearly straight ahead over the head: unturned,
        /// its left hand let go going 11–20° to the right of the line (CombatLab.Lanzar), so it turns 15° (BASTION_GIRO_GRANADA
        /// for the labs).
        /// </summary>
        static readonly float GrenadeTurn = LabFloat("BASTION_GIRO_GRANADA", 15);
        const float StoneTurn = 30;

        /// <summary>
        /// Before a throw (the 3D review's A-03, 10/11): its turn to the target nearly done (within AimTurnLeft), ThrowFace
        /// at most; it had begun the throw half way round, 67° to 134° off in the first frame.
        /// </summary>
        static async Awaitable FacedFirst(View v, CancellationToken ct)
        {
            for (float t = 0; t < ThrowFace && TurnLeft(v) > AimTurnLeft; t += Time.deltaTime) await Awaitable.NextFrameAsync(ct);
        }

        const float ThrowFace = 0.6f;

        /// <summary>`v` turned `degrees` to its left of where it is turning to (or faces), at the throw's pace.</summary>
        static void ThrowTurn(View v, float degrees)
        {
            float yaw = v.yawTo ?? v.root.eulerAngles.y;
            TurnTo(v, yaw - degrees, ThrowTurnRate);
        }

        const float ThrowTurnRate = 10;

        Vector3 LeftHand(string id) => Bone(id, HumanBodyBones.LeftHand) ?? Hand(id);

        /// <summary>
        /// A grenade's throw, by the free left hand (Mixamo's, mirrored); `onHit` as it leaves it (where the arc starts:
        /// ThrowHand(id)). `held` "molotov": the bottle in that hand, its rag lit, till it lets it go (Fx.Molotov; its arc
        /// is Fx.MolotovArc); "grenade", "smoke", "flash": that grenade (Fx.Thrown; its arc, Fx.GrenadeArc with the same).
        /// Turned GrenadeTurn to its left as it throws, and back as it lets go.
        /// </summary>
        public async Awaitable ThrowMotion(string id, Action onHit = null, CancellationToken ct = default, string held = null)
        {
            if (views.TryGetValue(id, out var v)) await FacedFirst(v, ct);
            if (v != null && v.anim != null && FigureCatalog.Load()?.Clip("throwGrenade") is AnimationClip toss) v.anim.PlayFree(toss, 0.1f, 1, GrenadeRelease - GrenadeLead);
            if (v != null) ThrowTurn(v, GrenadeTurn);
            Transform bottle = null;
            if (held != null && v?.animator != null && v.animator.isHuman && v.animator.GetBoneTransform(HumanBodyBones.LeftHand) is Transform hand)
            {
                // The molotov's bottle, or the grenade (the review of 9/11: POLYGON Military's frag, smoke or flash), in the left palm.
                bottle = held == "molotov" ? Effects.Fx.Molotov(hand) : Effects.Fx.Thrown(held, hand);
                // (In the palm, the neck up the way the fingers run: the middle finger's first bone from the hand's.)
                if (bottle != null && v.animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) is Transform finger)
                {
                    var along = finger.position - hand.position;
                    if (held == "molotov")
                    {
                        bottle.position = hand.position + along * 0.9f;
                        bottle.rotation = Quaternion.FromToRotation(Vector3.up, along.normalized);
                    }
                    else
                    {
                        // (A grenade by its middle, in the cup of the hand.)
                        var r = bottle.GetComponentInChildren<Renderer>();
                        var middle = r != null ? r.bounds.center : bottle.position;
                        bottle.position += hand.position + along * 0.75f - middle;
                    }
                }
            }
            try
            {
                await Awaitable.WaitForSecondsAsync(GrenadeLead, ct);
            }
            finally
            {
                if (bottle != null) DestroyNow(bottle.gameObject);
            }
            onHit?.Invoke();
            if (v != null) ThrowTurn(v, -GrenadeTurn);
            if (v != null) await ThrowDone(v, ct);
            else await Awaitable.WaitForSecondsAsync(ThrowAfter + ThrowBack, ct);
        }

        /// <summary>
        /// After the hand lets go, its follow-through for ThrowAfter, then the throw let go of over ThrowBack: the left hand
        /// back on the gun (the 3D review's A-16 and G-1, 10/11: the throws' clips went on to their ends, «Throw»'s arm hanging
        /// by the side and «Throw Object»'s 2.4 s more, the gun rigid in the right hand all that while).
        /// </summary>
        static async Awaitable ThrowDone(View v, CancellationToken ct)
        {
            await Awaitable.WaitForSecondsAsync(ThrowAfter, ct);
            v.anim?.StopFree(ThrowBack);
            await Awaitable.WaitForSecondsAsync(ThrowBack, ct);
        }

        const float ThrowAfter = 0.3f, ThrowBack = 0.25f;

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
                v.anim.PlayUpper(butt, WindUp, 1, HeadbuttLead);
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
            // (Those eating it get up off it, and those reaching for it let their arms down: Ana's #7, 1/11, the dead went on
            // eating where the body had been.)
            foreach (var other in views.Values.ToList())
            {
                if (other.reachFor == corpse) other.reachFor = null;
                if (other.feeding && other.feedOn == corpse) _ = GetUp(other, ct);
            }
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
