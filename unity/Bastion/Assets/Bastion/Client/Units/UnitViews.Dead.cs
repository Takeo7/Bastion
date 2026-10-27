using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The special dead's figures (syntyManifest.ts UNIT_BODIES, CHARACTERS): POLYGON's City Zombies and Boss Zombies, not
    /// Sidekick's, each the body its pack's prefab has on (the prefab holds every body of its rig) and what it keeps on;
    /// how every one of the dead leans and sways over its clips, and the Chillón's scream on its body.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>One of them: its pack's prefab, the body in it, its build, its size over the figures', the attachment it keeps.</summary>
        sealed class DeadBody
        {
            public string prefab;
            public string body;
            public bool female;
            public float scale = 1;
            public string keep;
        }

        /// <summary>A City Zombies character by its name (Jock_Male_01: SM_Chr_Zombie_Jock_Male_01 and its body Zombie_Jock_Male_01).</summary>
        static DeadBody City(string name, bool female = false, float scale = 1) =>
            new DeadBody { prefab = "SM_Chr_Zombie_" + name, body = "Zombie_" + name, female = female, scale = scale };

        /// <summary>
        /// The Corredores (the recently dead, still quick), the Acorazados (police and soldiers, vest and helmet still on, a
        /// little bigger), the Chillón (Boss Zombies' Wretch, a head taller than the rest) and the Hinchado (its Blobber), each
        /// of the bosses with its first hair and nothing else (the Blobber's prefab has a tiara on, 0.10.1's had none).
        /// </summary>
        static readonly Dictionary<string, DeadBody[]> DeadBodies = new Dictionary<string, DeadBody[]>
        {
            ["xenoid"] = new[] { City("Jock_Male_01"), City("Cheerleader_Female_01", true), City("Hoodie_Male_01"), City("SchoolGirl_Female_01", true) },
            ["mec"] = new[] { City("RiotCop_Male_01", false, 1.06f), City("Military_Male_01", false, 1.06f), City("Police_Male_01", false, 1.06f), City("Firefighter_Male_01", false, 1.06f) },
            ["sectoid"] = new[] { new DeadBody { prefab = "SM_Chr_ZombieBoss_Wretch_01", body = "SM_Chr_ZombieBoss_Wretch_01", scale = 1.12f, keep = "SM_Chr_Attach_Wretch_Hair_01" } },
            ["bloater"] = new[] { new DeadBody { prefab = "SM_Chr_ZombieBoss_Blobber_01", body = "SM_Chr_ZombieBoss_Blobber_01", scale = 1.08f, keep = "SM_Chr_Attach_Blobber_Hair_01" } },
        };

        /// <summary>
        /// Each pack material without the blood its shader lays over the atlas by a mask (PolygonGeneric's «Bloody» shaders:
        /// City Zombies' _Blood_Mask, Boss Zombies' _Blood_Map); 0.10.1 drew the atlas alone, its own stains in it. A copy each.
        /// </summary>
        static readonly Dictionary<Material, Material> bloodless = new Dictionary<Material, Material>();

        static readonly string[] BloodMasks = { "_Blood_Mask", "_Blood_Map" };

        static Material Bloodless(Material m)
        {
            if (m == null || !BloodMasks.Any(m.HasProperty)) return m;
            if (!bloodless.TryGetValue(m, out var copy) || copy == null)
            {
                copy = new Material(m) { name = m.name + " (sin sangre)" };
                foreach (string mask in BloodMasks)
                    if (copy.HasProperty(mask)) copy.SetTexture(mask, Texture2D.blackTexture);
                bloodless[m] = copy;
            }
            return copy;
        }

        /// <summary>The prefabs the special dead are drawn from (FigureCatalogMake puts them in the catalog's characters).</summary>
        public static IEnumerable<string> DeadPrefabs => DeadBodies.Values.SelectMany(b => b).Select(b => b.prefab).Distinct();

        /// <summary>A special one's figure, picked by its seed as 0.10.1's pick (its build and size in `view`); null if it isn't one or its prefab is missing.</summary>
        static GameObject DeadFigureOf(Unit u, View view, FigureCatalog catalog)
        {
            if (!DeadBodies.TryGetValue(u.Template, out var bodies)) return null;
            var b = bodies[PickIndex(bodies.Length, view.seed)];
            var prefab = catalog.Character(b.prefab);
            if (prefab == null) return null;
            var figure = Instantiate(prefab);
            figure.name = b.prefab;
            // Its body and what it keeps; the prefab's other bodies and attachments gone.
            foreach (var r in figure.GetComponentsInChildren<Renderer>(true).ToList())
            {
                bool kept = r.gameObject.name == b.body || r.gameObject.name == b.keep;
                r.gameObject.SetActive(kept);
                if (!kept && r.gameObject != figure) DestroyNow(r.gameObject);
                else r.sharedMaterials = r.sharedMaterials.Select(Bloodless).ToArray();
            }
            view.build = b.female ? "femn" : "masc";
            view.size = b.scale;
            return figure;
        }

        /// <summary>
        /// The Chillón's scream (unitViews.ts scream: its aim pose): the trunk and the head thrown back and both arms flung
        /// out and back, in over 0.18 s and held 0.55 s; done then, it lets go over 0.3 s more (the rings are Fx.Scream).
        /// </summary>
        public async Awaitable Scream(string id, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v))
            {
                await Awaitable.WaitForSecondsAsync(0.73f, ct);
                return;
            }
            for (float t = 0; t < 0.18f; t += Time.deltaTime)
            {
                v.scream = Mathf.SmoothStep(0, 1, t / 0.18f);
                await Awaitable.NextFrameAsync(ct);
            }
            v.scream = 1;
            await Awaitable.WaitForSecondsAsync(0.55f, ct);
            LetGo();
            async void LetGo()
            {
                for (float t = 0; t < 0.3f && v.scream > 0; t += Time.deltaTime)
                {
                    v.scream = 1 - Mathf.SmoothStep(0, 1, t / 0.3f);
                    await Awaitable.NextFrameAsync();
                }
                v.scream = 0;
            }
        }

        /// <summary>
        /// What each kind of the dead adds to its clips (syntyFigures.ts STYLES, gaits.ts): how far it leans forward (radians),
        /// how much its hips roll and its shoulders twist. The common dead drag themselves hunched; the Corredor goes bent;
        /// the Acorazado stands straighter and sways heavily; the Hinchado wobbles; the Chillón as any. And how much more it
        /// leans moving (`moving`, Unity's own): Goblin Locomotion's walk and run through Mecanim go straighter than through
        /// 0.10.1's retarget, by body (26/10, the trunk from the hips to the neck, the same body each side: the common
        /// zombie 70.7° against 82.0°, the Hinchado 40.6° against 61.9°, the Corredor 59.1° against 64.1°; standing, and the
        /// Acorazado and the Chillón moving, within 3°). A lean shows four fifths of itself in the trunk.
        /// </summary>
        static readonly Dictionary<string, (float lean, float roll, float twist, float moving)> DeadStyles = new Dictionary<string, (float, float, float, float)>
        {
            ["zombie"] = (0.5f, 1.5f, 1.3f, 0.25f),
            ["xenoid"] = (0.55f, 0.6f, 0.6f, 0.11f),
            ["mec"] = (0.2f, 1.7f, 0.4f, 0),
            ["bloater"] = (0.12f, 2.6f, 0.8f, 0.46f),
            ["sectoid"] = (0.38f, 1, 1, 0),
        };

        /// <summary>syntyFigures.ts CADENCE: steps a second of each motion (MOTION_ORDER) at its own speed, the hips' roll once a stride (π each).</summary>
        static readonly float[] RollRates = { 0, 0, Mathf.PI * 1.9f, Mathf.PI * 2.9f, Mathf.PI * 1.9f, Mathf.PI * 3.3f };
        static readonly float[] TwistRates = { 1.7f, 1.7f, 1.7f, 1.7f, 1.7f, 1.7f };

        /// <summary>
        /// One of the dead over what its clips have it in (syntyFigures.ts applyPose, for a body moving by its clips): its
        /// lean spread up the spine (0.35, 0.7 and all of it by the bone, each turned in the body's frame from the clip's
        /// own), the hips rolling and the shoulders twisting in step with its clips, the neck lolled to the side and back
        /// up by 0.55 of the lean so the eyes stay up; the legs as the clip has them. The Chillón's scream (`v.scream`)
        /// throws the lean back half a radian and the neck 0.6 more, and the arms out. None while it dies (a death clip
        /// takes the body: `live`).
        /// </summary>
        static void DeadPose(View v)
        {
            var a = v.animator;
            if (a == null || !a.isHuman || v.anim == null) return;
            float live = v.dead ? 0 : 1, k = v.scream;
            if (live <= 0 && k <= 0) return;
            var style = DeadStyles.TryGetValue(v.template, out var s) ? s : DeadStyles["sectoid"];
            float lean = (style.lean + style.moving * v.anim.Moving) * live - k * 0.5f;
            float twist = v.anim.Sway(1.7f, TwistRates) * 0.12f * style.twist * live;
            float roll = v.anim.Sway(1.3f, RollRates) * (0.05f + v.anim.Moving * 0.08f) * style.roll * live;
            var body = v.root.rotation;
            // A turn in the body's frame (rot Z by roll, then Y by twist, then X by lean: forward is a lean over).
            Quaternion Turn(float r, float t, float l) => Quaternion.AngleAxis(r * Mathf.Rad2Deg, Vector3.forward) * Quaternion.AngleAxis(t * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(l * Mathf.Rad2Deg, Vector3.right);
            Quaternion World(Quaternion turn) => body * turn * Quaternion.Inverse(body);
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips), legL = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), legR = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var spine = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }.Select(a.GetBoneTransform).Where(b => b != null).ToList();
            Transform neck = a.GetBoneTransform(HumanBodyBones.Neck) ?? a.GetBoneTransform(HumanBodyBones.Head);
            // The clip's turns, before any is changed (each bone is set from its own: the body's frame, as 0.10.1's).
            Quaternion? wasL = legL != null ? legL.rotation : (Quaternion?)null, wasR = legR != null ? legR.rotation : (Quaternion?)null;
            var was = spine.Select(b => b.rotation).ToList();
            var wasNeck = neck != null ? neck.rotation : Quaternion.identity;
            if (hips != null) hips.rotation = World(Turn(roll, 0, 0)) * hips.rotation;
            if (legL != null) legL.rotation = wasL.Value;
            if (legR != null) legR.rotation = wasR.Value;
            // (0.10.1's three: 0.35, 0.7 and 1 of the lean, 0.3, 0.6 and 1 of the twist; fewer bones, the last takes all.)
            float[] leans = spine.Count >= 3 ? new[] { 0.35f, 0.7f, 1f } : spine.Count == 2 ? new[] { 0.5f, 1f } : new[] { 1f };
            float[] twists = spine.Count >= 3 ? new[] { 0.3f, 0.6f, 1f } : spine.Count == 2 ? new[] { 0.5f, 1f } : new[] { 1f };
            var upper = Quaternion.identity;
            for (int i = 0; i < spine.Count; i++)
            {
                upper = Turn(roll, twist * twists[i], lean * leans[i]);
                spine[i].rotation = World(upper) * was[i];
            }
            if (neck != null)
            {
                var head = upper * Quaternion.AngleAxis(0.32f * live * Mathf.Rad2Deg, Vector3.forward) * Quaternion.AngleAxis((-lean * 0.55f - k * 0.6f) * Mathf.Rad2Deg, Vector3.right);
                neck.rotation = World(head) * wasNeck;
            }
            DeadArms(v);
            if (k <= 0) return;
            // (0.10.1's model x is to its left, Unity's −x, and its z behind it, Unity's −z: its own screams, seen from
            // the game's camera and from the side, have the arms out wide and ahead of it.)
            FlingArm(a, v.root, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(-0.85f, -0.15f, 0.4f), new Vector3(-0.8f, 0.25f, 0.35f), k);
            FlingArm(a, v.root, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(0.85f, -0.15f, 0.4f), new Vector3(0.8f, 0.25f, 0.35f), k);
        }

        /// <summary>The bone 0.10.1 calls Spine_03: the upper chest, or the chest where there is none.</summary>
        static Transform ChestOf(Animator a) => a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest);

        /// <summary>syntyFigures.ts SOFT_REACH: the last of an arm's reach eased, so the elbow bends in smoothly (m, the model's).</summary>
        const float SoftReach = 0.025f;
        /// <summary>The least of an arm's length a dead one's hand reaches out to (DeadArms).</summary>
        const float StraightArm = 0.985f;

        /// <summary>
        /// syntyFigures.ts applyPose, the dead: arms out in front, swaying, raised further on a lunge and closed on its blow;
        /// each hand 0.4 ahead of the upper chest and 0.02 under it (the model's metres, its rest), 0.12 out to its side,
        /// where the chest has turned since its rest; reached by two-bone IK, the elbow out, down and back (the pole). Not
        /// while a clip takes the whole body (a bite, eating, getting up) nor fallen.
        /// </summary>
        /// <summary>For the labs: the dead's arms left as their clips have them (to tell what DeadArms does).</summary>
        public static bool DeadArmsOff;

        static void DeadArms(View v)
        {
            var a = v.animator;
            if (DeadArmsOff || v.dead || v.feeding || v.chestRest is not Quaternion rest || v.anim.WholeShare > 0.01f) return;
            var chest = ChestOf(a);
            if (chest == null) return;
            float scale = v.body != null ? v.body.lossyScale.x : 1;
            var turn = chest.rotation * Quaternion.Inverse(v.root.rotation * rest);
            float aim = v.reachAim, grab = v.reachGrab;
            foreach (var (s, upper, lower, hand) in new[] { (1f, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (-1f, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
            {
                float sway = v.anim.Sway(1.6f, ArmRates, s) * 0.05f;
                // (0.10.1's model x is to its left: Unity's −x.)
                var offset = new Vector3(-s * 0.12f * (1 - 0.7f * grab), -0.02f + aim * 0.12f + sway + grab * 0.05f, 0.4f + aim * 0.08f + grab * 0.12f) * scale;
                var target = chest.position + turn * (v.root.rotation * offset);
                var pole = v.root.rotation * new Vector3(-s * 0.8f, -0.7f, -0.3f);
                Reach(a, upper, lower, hand, target, pole, SoftReach * scale, true);
            }
        }

        static readonly float[] ArmRates = { 1.6f, 1.6f, 1.6f, 1.6f, 1.6f, 1.6f };

        /// <summary>
        /// Two-bone IK (syntyFigures.ts reach): the hand to `target`, the elbow bent towards `pole`, the arm's lengths kept;
        /// `straight`, out at least StraightArm of its length (the dead's arms).
        /// </summary>
        static void Reach(Animator a, HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 target, Vector3 pole, float soft, bool straight = false)
        {
            Transform shoulder = a.GetBoneTransform(upperBone), elbow = a.GetBoneTransform(lowerBone), hand = a.GetBoneTransform(handBone);
            if (shoulder == null || elbow == null || hand == null) return;
            float upperLength = Vector3.Distance(shoulder.position, elbow.position), lowerLength = Vector3.Distance(elbow.position, hand.position);
            var dir = target - shoulder.position;
            // (Out as far as the arm goes, near enough straight, as 0.10.1's dead hold them, 160–173° at the elbow, whatever the
            // body: City Zombies' and Boss Zombies' arms are longer than the 0.4 ahead reaches for a Sidekick's.)
            if (straight && dir.magnitude < (upperLength + lowerLength) * StraightArm) dir = dir.normalized * (upperLength + lowerLength) * StraightArm;
            float full = upperLength + lowerLength, len = dir.magnitude;
            if (len > full - soft) len = full - soft * Mathf.Exp(-(len - (full - soft)) / Mathf.Max(soft, 1e-5f));
            len = Mathf.Clamp(len, Mathf.Abs(upperLength - lowerLength) + 0.01f, full - 0.001f);
            dir.Normalize();
            float cos = Mathf.Clamp((upperLength * upperLength + len * len - lowerLength * lowerLength) / (2 * upperLength * len), -1, 1);
            // The elbow in the plane of the arm and the pole, on the pole's side.
            var side = Vector3.ProjectOnPlane(pole, dir);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.ProjectOnPlane(Vector3.down, dir);
            side.Normalize();
            var elbowAt = shoulder.position + (dir * cos + side * Mathf.Sqrt(1 - cos * cos)) * upperLength;
            var handAt = shoulder.position + dir * len;
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, elbowAt - shoulder.position) * shoulder.rotation;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, handAt - elbow.position) * elbow.rotation;
        }

        /// <summary>A lunge's arms (unitViews.ts lunge: aimFx up as it winds up, swing −1 to 1 through the stroke, back over 0.35 s; grab |swing|).</summary>
        static async Awaitable LungeArms(View v, System.Threading.CancellationToken ct)
        {
            try
            {
                float t = 0;
                for (; t < 0.8f && v.root != null; t += Time.deltaTime)
                {
                    // (lunge's own: wound up over 0.2 s, the stroke 0.14 s, back over 0.35 s.)
                    float hit = WindUp + StrokeTime;
                    float wind = Mathf.Clamp01(t / WindUp), stroke = Mathf.Clamp01((t - WindUp) / StrokeTime), back = Mathf.Clamp01((t - hit) / Back);
                    float swing = t < WindUp ? -Mathf.SmoothStep(0, 1, wind) : t < hit ? -1 + 2 * Mathf.SmoothStep(0, 1, stroke) : 1 - Mathf.SmoothStep(0, 1, back);
                    v.reachAim = t < hit ? wind : 1 - back;
                    v.reachGrab = Mathf.Abs(swing);
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            catch (System.OperationCanceledException) { }
            v.reachAim = v.reachGrab = 0;
        }

        /// <summary>syntyFigures.ts setArm, blended in by `k`: the upper arm along `upper`, the forearm along `fore` (the figure's frame).</summary>
        static void FlingArm(Animator a, Transform root, HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 upper, Vector3 fore, float k)
        {
            Transform shoulder = a.GetBoneTransform(upperBone), elbow = a.GetBoneTransform(lowerBone), hand = a.GetBoneTransform(handBone);
            if (shoulder == null || elbow == null || hand == null) return;
            var to = Quaternion.FromToRotation(elbow.position - shoulder.position, root.TransformDirection(upper.normalized));
            shoulder.rotation = Quaternion.Slerp(Quaternion.identity, to, k) * shoulder.rotation;
            to = Quaternion.FromToRotation(hand.position - elbow.position, root.TransformDirection(fore.normalized));
            elbow.rotation = Quaternion.Slerp(Quaternion.identity, to, k) * elbow.rotation;
        }
    }
}
