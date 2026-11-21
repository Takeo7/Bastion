using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The bow (Arturo's cuadros 101–103: Synty's recurve, ANIMATION - Bow Combat): in the left hand on the rig's own prop
    /// bone (prop_l, as the pack's clips expect it), its string drawn by the right hand's fingers while it aims (BowString)
    /// with an arrow on it, let go with the shot; a quiver at the back. At rest and aimed in the pack's clips over the
    /// body (AtRest, Aimed); as it moves, its arms its own.
    /// </summary>
    public sealed partial class UnitViews
    {
        public const string ArrowModel = "Wep_Arrow_Modern_01", QuiverModel = "Wep_Quiver_01";

        /// <summary>The bow across the back: its length (+y) along the mount's line up the back, its string to the back.</summary>
        static readonly Quaternion BowOnBack = Quaternion.Euler(LabVector("BASTION_ARCO_ESPALDA") ?? new Vector3(90, 0, 90));
        /// <summary>
        /// The quiver on the back, in the body's frame: to the right of the spine, this far behind the back's surface (over a
        /// rucksack, behind it, as the slung gun: Stows), leant so (degrees: its mouth up over the right shoulder).
        /// </summary>
        static readonly Vector3 QuiverAt = LabVector("BASTION_CARCAJ_EN") ?? new Vector3(0.09f, -0.12f, 0.02f), QuiverTurn = LabVector("BASTION_CARCAJ_GIRO") ?? new Vector3(0, 0, -22);
        /// <summary>Seconds after the shot with no arrow on the string (the hand goes to the quiver for the next).</summary>
        const float LooseFor = 0.55f;

        static readonly Dictionary<Material, Material> bowMaterials = new Dictionary<Material, Material>();

        /// <summary>The pack's material for our pipeline (its own is the built-in one's: pink under URP): Kit/Plain with its texture.</summary>
        static Material BowMaterial(Material from)
        {
            if (from == null) return null;
            if (bowMaterials.TryGetValue(from, out var made) && made != null) return made;
            Texture texture = null;
            foreach (string name in from.GetTexturePropertyNames())
                if ((texture = from.GetTexture(name)) != null) break;
            made = KitMaterials.New(KitMaterials.Plain, from.name + " (arco)");
            if (made == null) return from;
            if (texture != null) made.SetTexture("_BaseMap", texture);
            return bowMaterials[from] = made;
        }

        /// <summary>What goes with a bow as its main weapon: its string's joint and an arrow for it, and the quiver at the back.</summary>
        void BowKit(View v, FigureCatalog catalog)
        {
            if (!v.arts.TryGetValue(MainCarry, out var art) || !art.bow || !v.carried.TryGetValue(MainCarry, out var bow)) return;
            var parts = bow.GetComponentsInChildren<Transform>(true);
            v.cord = bow.gameObject.AddComponent<BowString>();
            v.cord.enabled = false;
            v.cord.joint = parts.FirstOrDefault(p => p.name == "string_jnt");
            v.cord.rest = parts.FirstOrDefault(p => p.name == "arrow_rest_jnt");
            v.cord.fingers = v.animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal) ?? v.animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (catalog.Weapon(ArrowModel) is GameObject arrow)
            {
                var made = Instantiate(arrow, v.root, false);
                made.name = "flecha";
                foreach (var r in made.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = BowMaterial(r.sharedMaterial);
                made.transform.localScale = Vector3.one * v.body.lossyScale.x / Mathf.Max(v.root.lossyScale.x, 1e-4f);
                made.SetActive(false);
                v.cord.arrow = made.transform;
            }
            var chest = v.animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? v.animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest != null && catalog.Weapon(QuiverModel) is GameObject quiver)
            {
                v.quiver = Instantiate(quiver).transform;
                v.quiver.name = "carcaj";
                foreach (var r in v.quiver.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = BowMaterial(r.sharedMaterial);
                var body = v.body;
                float scale = body.lossyScale.x;
                float pack = body.GetComponent<SoldierMarks>() is SoldierMarks marks ? marks.pack : 0;
                float behind = Mathf.Max(0.11f, pack + PackGap) + QuiverAt.z;
                var at = body.InverseTransformPoint(chest.position) + new Vector3(QuiverAt.x, QuiverAt.y, -behind);
                v.quiver.localScale = Vector3.one * scale;
                v.quiver.SetPositionAndRotation(body.TransformPoint(at), body.rotation * Quaternion.Euler(QuiverTurn));
                v.quiver.SetParent(chest, true);
                if (BowLog)
                {
                    var box = v.quiver.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    var c = body.InverseTransformPoint(box.center);
                    Debug.Log(System.FormattableString.Invariant($"[arco] {v.id}: quiver's middle in the body {c.x:0.00}, {c.y:0.00}, {c.z:0.00}, box {box.size.x:0.00} × {box.size.y:0.00} × {box.size.z:0.00}; chest {body.InverseTransformPoint(chest.position).z:0.00}, pack {pack:0.00}, behind {behind:0.00}"));
                }
            }
        }

        static readonly bool BowLog = Application.isEditor && System.Environment.GetEnvironmentVariable("BASTION_ARCO_LOG") == "1";

        /// <summary>The bow in the left hand, on the rig's prop bone; its string and its arrow by what it does.</summary>
        static void HoldBow(View v, float dt)
        {
            var prop = v.bowProp != null ? v.bowProp : v.bowProp = v.body.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name == "prop_l") ?? v.animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (prop == null) return;
            if (v.weapon.parent != prop)
            {
                v.weapon.SetParent(prop, false);
                v.weapon.localPosition = Vector3.zero;
                v.weapon.localRotation = Quaternion.identity;
                v.weapon.localScale = Vector3.one;
            }
            if (v.cord == null) return;
            v.loosed += dt;
            bool up = Up(v) && !v.dead && v.loosed > LooseFor;
            v.cord.pull = up && v.up > 0.5f;
            v.cord.nocked = up;
            v.cord.ShotStep(dt);
        }

        /// <summary>A crossbow's loading (seconds): its nose down by, the hand at the string, drawn back, a bolt laid on, up again.</summary>
        const float CockSeconds = 1.7f;
        static readonly float[] CockBeats = { 0.3f, 0.55f, 1.0f, 1.3f, 1.7f };
        /// <summary>How far its nose goes down (degrees), and along it from its grip: where the string rests, and where it locks (metres of its frame, unscaled).</summary>
        const float CockDown = 58, CockFrom = 0.42f, CockTo = 0.12f, CockOver = 0.06f;

        /// <summary>
        /// The crossbow as it is loaded, over where the right hand holds it (after Hold, the left hand off it): turned nose
        /// down before the body about the right hand, the left hand to its string, drawn back along it to the lock, then up
        /// over it (the bolt laid on) and back to its fore-end as it comes up.
        /// </summary>
        static void Cock(View v)
        {
            var a = v.animator;
            Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand), upper = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), lower = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), left = a.GetBoneTransform(HumanBodyBones.LeftHand);
            if (hand == null || upper == null || lower == null || left == null) return;
            float t = v.cock;
            float down = t < CockBeats[0] ? Mathf.SmoothStep(0, 1, t / CockBeats[0]) : t < CockBeats[3] ? 1 : Mathf.SmoothStep(1, 0, (t - CockBeats[3]) / (CockBeats[4] - CockBeats[3]));
            v.weapon.RotateAround(hand.position, v.root.right, CockDown * down);
            float scale = v.weapon.lossyScale.x;
            // Where the left hand is along it: to the string, back with it, over it, and away.
            float along = t < CockBeats[1] ? Mathf.Lerp(CockFrom + 0.1f, CockFrom, Mathf.SmoothStep(0, 1, t / CockBeats[1]))
                : t < CockBeats[2] ? Mathf.Lerp(CockFrom, CockTo, Mathf.SmoothStep(0, 1, (t - CockBeats[1]) / (CockBeats[2] - CockBeats[1])))
                : Mathf.Lerp(CockTo, CockFrom * 0.7f, Mathf.SmoothStep(0, 1, (t - CockBeats[2]) / (CockBeats[3] - CockBeats[2])));
            float over = t > CockBeats[2] && t < CockBeats[3] ? Mathf.Sin((t - CockBeats[2]) / (CockBeats[3] - CockBeats[2]) * Mathf.PI) * 0.08f : 0;
            var at = v.weapon.position + v.weapon.forward * along * scale + v.weapon.up * (CockOver + over) * scale;
            Grasp(upper, lower, left, at, -v.root.right - Vector3.up);
            Fist(a, true);
        }

        /// <summary>The shot: the string let go, the pack's own loose and reach for the next arrow over the upper body.</summary>
        static void Loose(View v)
        {
            v.loosed = 0;
            var clip = FigureCatalog.Load()?.Clip("bowShoot");
            v.anim?.PlayUpper(clip, 0.03f);
            v.kickLeft = clip != null ? clip.length : 0;
        }
    }
}
