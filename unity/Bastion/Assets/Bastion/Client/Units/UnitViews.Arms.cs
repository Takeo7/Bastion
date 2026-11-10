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
    /// The soldiers' weapons as 0.10.1 carried and used them (syntyFigures.ts armamentOf, hangStows, hold, draw, direct,
    /// strike; unitViews.ts raiseWeapon, lunge, reload): the main gun in the hands, a pistol in its holster down the right
    /// thigh, a blade at the belt on the left; each drawn when the act needs it (the pistol to fire it, the blade to
    /// strike), and the others where they ride meanwhile (the main gun slung across the back; a pistol as the only gun, in
    /// the holster). Model space as 0.10.1's with its x turned round: Unity's figure faces +z with its right on +x.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>syntyFigures.ts SQUAD: the templates a campaign's arms dress.</summary>
        static readonly HashSet<string> SquadTemplates = new HashSet<string> { "rookie", "assault", "grenadier", "sharpshooter", "specialist" };

        /// <summary>Where each weapon rides (syntyFigures.ts CARRIES): the main one, the pistol, the blade.</summary>
        const string MainCarry = "weapon", SidearmCarry = "sidearm", BeltCarry = "belt", SlungCarry = "slung";

        /// <summary>The holster this far below the hip (syntyFigures.ts HOLSTER_DROP); the blade out from the hips this way and this far (BELT_OUT, without a body to keep off).</summary>
        const float HolsterDrop = 0.15f, BeltOutBy = 0.13f;
        /// <summary>Where along it the chainsaw hangs on the back, of the way from its back handle to its middle.</summary>
        const float SlungBy = 0.35f;
        static readonly Vector3 BeltOut = new Vector3(-0.62f, 0, -0.78f).normalized, BeltAlong = new Vector3(-0.1f, -1, -0.45f).normalized;

        /// <summary>The pistol at rest, «Pistol Aim» held this far in (syntyFigures.ts PISTOL_HOLD: one-handed by the thigh).</summary>
        public const float PistolHold = 6.4f;

        /// <summary>How one waits with the pistol (syntyFigures.ts PISTOL_RESTS): the stance for the rifle's way of waiting it would have had (RestFor).</summary>
        public static string PistolStance(string rifleRest) => rifleRest != null && PistolRests.TryGetValue(rifleRest, out var stance) ? stance : "restBase";

        static readonly Dictionary<string, string> PistolRests = new Dictionary<string, string> { ["rifleIdle"] = "restBase", ["rifleLowered"] = "handsOnHips", ["rifleIdleLook"] = "thoughtfulL" };

        /// <summary>
        /// syntyFigures.ts armamentOf: the main weapon, the pistol (sidearm) and the blade (belt) one carries. A squad
        /// soldier's from its arms: the Armería's gun at its tier (else the pistol as its main gun, and then no sidearm), its
        /// blade (else the knife); without a pistol (Arms.Pistol false: Arturo, 1/11, nobody starts with one) none in the
        /// holster, and with no gun at all its blade in the hand, nothing at the belt. Anyone else's from its template:
        /// UNIT_WEAPONS at its tier, the pistol as a second gun, a squad soldier's machete for a sword, the Cazador's baton
        /// for its stun lance.
        /// </summary>
        static (string weapon, string sidearm, string belt) ArmamentOf(Unit u)
        {
            int tier = Mathf.Clamp(u.WeaponTier ?? 1, 1, 3);
            if (u.Id != null && ArtsForLab.TryGetValue(u.Id, out var lab)) return (lab, null, null);
            if (u.Arms != null && SquadTemplates.Contains(u.Template))
            {
                string primary = u.Arms.Primary;
                string blade = u.Arms.Melee != null ? Anims.FoundModel(u.Arms.Melee, tier) : "knife";
                bool pistol = Content.HasPistol(u);
                if (primary != null) return (Anims.FoundModel(primary, tier), pistol ? "pistol" : null, blade);
                return pistol ? ("pistol", null, blade) : (blade, null, null);
            }
            Content.Templates.TryGetValue(u.Template ?? "", out var t);
            return (Anims.UnitWeapon(u.Template, tier),
                t != null && t.Secondary == "pistol" && t.Weapon != "pistol" ? "pistol" : null,
                t != null && SquadTemplates.Contains(u.Template) && t.MeleeWeapon == "sword" ? "machete"
                // The Cazador's stun lance at its belt, its carbine in its hands (the review of 9/11: the baton was its main
                // weapon, and it fired the carbine from its tip, unaimed; 0.10.1 the same).
                : t != null && t.MeleeWeapon == WeaponIds.StunLance ? "baton" : null);
        }

        /// <summary>The pistol's model under `parent`, at the figures' scale (the Settlement's soldiers whose only gun it is).</summary>
        public static Transform Pistol(Transform parent, FigureCatalog catalog) => WeaponModel(Anims.Weapon("pistol"), parent, catalog, out _);

        /// <summary>Whether a soldier of these arms carries a pistol (Content.HasPistol: arms from before carry one; Arturo, 1/11: nobody starts with one).</summary>
        public static bool CarriesPistol(Arms arms) => arms == null || arms.Pistol != false;

        /// <summary>
        /// Where the pistol rides in its holster down the right thigh (hangStows' sidearm), for a figure (`body` its model,
        /// at the pose it stands in): the thigh's bone, and the place and turn in its frame.
        /// </summary>
        public static (Transform bone, Vector3 at, Quaternion turn)? Holster(Animator a, Transform body)
        {
            var bone = a != null ? a.GetBoneTransform(HumanBodyBones.RightUpperLeg) : null;
            if (bone == null || body == null) return null;
            var hipR = body.InverseTransformPoint(bone.position);
            var world = body.TransformPoint(new Vector3(hipR.x + 0.075f, hipR.y - HolsterDrop, hipR.z + 0.01f));
            var turn = body.rotation * Quaternion.LookRotation(new Vector3(0, -1, 0.1f), new Vector3(0, 0, 1));
            return (bone, bone.InverseTransformPoint(world), Quaternion.Inverse(bone.rotation) * turn);
        }

        /// <summary>A weapon's model (any: a gun, the pistol, a blade) under `parent`, at the figures' scale, its muzzle in its own frame.</summary>
        static Transform WeaponModel(WeaponArt art, Transform parent, FigureCatalog catalog, out Vector3 muzzle)
        {
            muzzle = Vector3.zero;
            var prefab = art != null && catalog != null ? catalog.Weapon(art.fbx) : null;
            if (prefab == null) return null;
            var model = Instantiate(prefab, parent, false);
            model.name = "arma";
            // (Apocalypse's silencers, and a sword's sheath: Gang Warfare's comes on it.)
            foreach (var part in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Silencer") || t.name.Contains("Sheath")).ToList()) part.gameObject.SetActive(false);
            var box = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                foreach (var corner in Corners(r.bounds)) box.Encapsulate(model.transform.InverseTransformPoint(corner));
            muzzle = art.muzzle != null && art.muzzle.Length >= 3 ? new Vector3(art.muzzle[0], art.muzzle[1], art.muzzle[2]) : new Vector3(0, 0.02f, box.max.z);
            model.transform.localScale = Vector3.one * Figures.CharacterScale * (art.size > 0 ? art.size : 1);
            foreach (var c in model.GetComponentsInChildren<Collider>()) DestroyNow(c);
            return model.transform;
        }

        /// <summary>The middle of what a model draws, in its own frame, unscaled.</summary>
        static Vector3 CentreOf(Transform model)
        {
            var box = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                foreach (var corner in Corners(r.bounds))
                {
                    var p = model.InverseTransformPoint(corner);
                    if (!any) box = new Bounds(p, Vector3.zero);
                    else box.Encapsulate(p);
                    any = true;
                }
            return any ? box.center : Vector3.zero;
        }

        /// <summary>The back of a model in its own frame, unscaled (its butt: the middle of its box across, the least along +z).</summary>
        static Vector3 ButtOf(Transform model)
        {
            var box = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                foreach (var corner in Corners(r.bounds))
                {
                    var p = model.InverseTransformPoint(corner);
                    if (!any) box = new Bounds(p, Vector3.zero);
                    else box.Encapsulate(p);
                    any = true;
                }
            return any ? new Vector3(box.center.x, box.center.y, box.min.z) : Vector3.zero;
        }

        /// <summary>A model's middle along its barrel or blade (syntyFigures.ts equip's userData.middle), in its own frame scaled: a slung gun hangs by it.</summary>
        static float MiddleOf(Transform model)
        {
            var box = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                foreach (var corner in Corners(r.bounds))
                {
                    var p = model.InverseTransformPoint(corner);
                    if (!any) box = new Bounds(p, Vector3.zero);
                    else box.Encapsulate(p);
                    any = true;
                }
            return any ? box.center.z * model.localScale.z : 0;
        }

        /// <summary>
        /// Its weapons (ArmamentOf) made and put where they ride (Stows), the main one drawn; how it waits with it (one of
        /// RIFLE_RESTS by its seed, or with the pistol one of PISTOL_RESTS). False without a figure or anything to carry.
        /// </summary>
        bool Arm(View v, Unit u, FigureCatalog catalog)
        {
            if (v.animator == null || !v.animator.isHuman || catalog == null) return false;
            var (weapon, sidearm, belt) = ArmamentOf(u);
            foreach (var (carry, id) in new[] { (MainCarry, weapon), (SidearmCarry, sidearm), (BeltCarry, belt) })
            {
                var art = Anims.Weapon(id);
                var muzzle = Vector3.zero;
                var model = art != null ? WeaponModel(art, v.root, catalog, out muzzle) : null;
                if (model == null) continue;
                v.carried[carry] = model;
                v.arts[carry] = art;
                v.muzzles[carry] = muzzle;
                v.middles[carry] = MiddleOf(model);
                v.butts[carry] = ButtOf(model);
            }
            if (v.carried.Count == 0) return false;
            Stows(v);
            v.rest = RestFor(v.seed);
            if (SheathOn) Sheathe(v, catalog);
            KeepOff(v);
            Draw(v, MainCarry);
            return true;
        }

        /// <summary>Whether its main weapon is a two-handed gun (it moves and waits in the rifle's clips).</summary>
        static bool Rifled(View v) => v.arts.TryGetValue(MainCarry, out var a) && !a.pistol && !a.melee;

        /// <summary>Whether its main weapon is the pistol (its own way of waiting, its arms its own as it moves).</summary>
        static bool PistolMain(View v) => v.arts.TryGetValue(MainCarry, out var a) && a.pistol;

        /// <summary>Whether its main weapon is a blade: no gun at all (Arms.Pistol false, nothing from the Armería).</summary>
        static bool BladeMain(View v) => v.arts.TryGetValue(MainCarry, out var a) && a.melee;

        /// <summary>
        /// How it stands (syntyFigures.ts: the dead drunk, the living enemy aggressive): with the pistol one of
        /// PISTOL_RESTS; with only a blade, Sword Combat's blade held low (Arturo, 1/11: soldiers without a gun, not the
        /// pistol's); else its idle (null).
        /// </summary>
        static string StanceOf(View v, Unit u, bool armed) =>
            v.deadKind ? "drunk" : LivingEnemies.Contains(u.Template) ? "aggressive"
            : armed && PistolMain(v) && PistolRests.TryGetValue(v.rest ?? "", out var waits) ? waits
            : armed && BladeMain(v) ? "blade" : null;

        /// <summary>
        /// syntyFigures.ts hangStows: a place for each carry on its bone, at the pose it is made in. The main gun across the
        /// back from the chest, its barrel up over the right shoulder; the pistol down the right thigh, its muzzle down;
        /// the blade by the hips on the left, round towards the back, pointing down and back.
        /// </summary>
        static void Stows(View v)
        {
            var a = v.animator;
            var body = v.body;
            Vector3 In(HumanBodyBones b) => a.GetBoneTransform(b) is Transform t ? body.InverseTransformPoint(t.position) : Vector3.zero;
            void Hang(string carry, HumanBodyBones bone, Vector3 at, Vector3 along, Vector3 top)
            {
                var b = a.GetBoneTransform(bone);
                if (b == null) return;
                var mount = new GameObject("lleva " + carry).transform;
                mount.SetParent(b, false);
                mount.SetPositionAndRotation(body.TransformPoint(at), body.rotation * Quaternion.LookRotation(along, top));
                v.stows[carry] = mount;
            }
            var chest = a.GetBoneTransform(HumanBodyBones.UpperChest) != null ? HumanBodyBones.UpperChest : HumanBodyBones.Chest;
            var s3 = In(chest);
            // (Over a rucksack, out behind it: SoldierMarks.pack; Arturo, 9/11, the 0.12.15's rucksacks: never through it.)
            float pack = body.GetComponent<SoldierMarks>() is SoldierMarks marks ? marks.pack : 0;
            float back = Mathf.Max(0.11f, pack + PackGap);
            Hang(MainCarry, chest, new Vector3(0, s3.y + 0.02f, s3.z - back), new Vector3(0.45f, 0.9f, 0), new Vector3(0, 0, -1));
            var hipR = In(HumanBodyBones.RightUpperLeg);
            Hang(SidearmCarry, HumanBodyBones.RightUpperLeg, new Vector3(hipR.x + 0.075f, hipR.y - HolsterDrop, hipR.z + 0.01f), new Vector3(0, -1, 0.1f), new Vector3(0, 0, 1));
            var hips = In(HumanBodyBones.Hips);
            var hipL = In(HumanBodyBones.LeftUpperLeg);
            Hang(BeltCarry, HumanBodyBones.Hips, new Vector3(hips.x, hipL.y + 0.03f, hips.z) + BeltOut * BeltOutBy, BeltAlong, Vector3.Cross(BeltOut, BeltAlong));
            // (A blade too big for the belt, the chainsaw: across the back the other way, over the left shoulder, behind the gun.)
            Hang(SlungCarry, chest, new Vector3(0, s3.y - 0.02f, s3.z - back - 0.09f), new Vector3(-0.45f, 0.9f, 0), new Vector3(0, 0, -1));
        }

        /// <summary>
        /// Where the slung gun's middle rides against a rucksack's farthest point along its line (model units): within it, as
        /// the gun lies across the back slanting, and that point is off its middle (a flask, a rolled mat), as the bare back's
        /// 0.19 to the gun's 0.11. Looked at from behind and from the side over the four rucksacks (9/11): further out it hung
        /// in the air, at −0.1 it lies on them and goes through none.
        /// </summary>
        static readonly float PackGap = LabFloat("BASTION_HOLGURA_MOCHILA", -0.1f);

        /// <summary>
        /// syntyFigures.ts draw and hold: `which` in the hands (one it doesn't carry: the main one), the others where they
        /// ride (the main gun on the back, or in the holster when it is the only gun, a pistol); at once, as 0.10.1 did.
        /// </summary>
        static void Draw(View v, string which)
        {
            if (!v.carried.ContainsKey(which)) which = MainCarry;
            if (!v.carried.ContainsKey(which)) which = v.carried.Keys.FirstOrDefault();
            var was = v.drawn;
            v.drawn = which;
            bool pistolOnly = PistolMain(v) && !v.carried.ContainsKey(SidearmCarry);
            foreach (var kv in v.carried)
            {
                var model = kv.Value;
                if (kv.Key == which)
                {
                    model.SetParent(v.root, false);
                    model.localScale = Vector3.one * Figures.CharacterScale * Size(v.arts[kv.Key]);
                    continue;
                }
                string ride = RideOf(v, kv.Key, pistolOnly);
                if (!v.stows.TryGetValue(ride, out var mount))
                {
                    model.gameObject.SetActive(false);
                    continue;
                }
                // (In a holster the outfit has with its own pistol in it, ours not drawn: F-08, Arturo's A. The knife at the
                // belt in its sheath: F-07, Arturo's A.)
                model.gameObject.SetActive(!(ride == SidearmCarry && OwnHolster(v)) && !(ride == BeltCarry && v.sheath != null));
                Ride(v, kv.Key, model, ride, mount);
            }
            v.weapon = v.carried.TryGetValue(which, out var held) ? held : null;
            if (v.weapon != null) v.weapon.gameObject.SetActive(true);
            v.muzzle = v.muzzles.TryGetValue(which, out var m) ? m : Vector3.zero;
            v.butt = v.butts.TryGetValue(which, out var b) ? b : Vector3.zero;
            v.inHand = v.arts.TryGetValue(which, out var art) ? art.pistol ? Held.Pistol : art.melee ? Held.Blade : Held.Rifle : Held.None;
            // (The rifle's turn in the hand kept: drawn again, it is held as it was, not level at the waist for a frame.)
            v.pistolGrip = null;
            // A long gun back in the hands from somewhere else: rigid in the right one as it was held, and onto the left over
            // TwoHandsBack while the rest comes back (Settle), not towards the left hand still where the blade left it (the
            // 3D review's G-1, 10/11: the gun went into the belly).
            if (which != was && v.inHand == Held.Rifle) v.twoHands = 0;
            // In the hand at once (else, for the frame, where the root is: at the feet).
            HoldNow(v);
        }

        static float Size(WeaponArt art) => art != null && art.size > 0 ? art.size : 1;

        /// <summary>Where `key`'s weapon rides when not in the hands: the pistol as the only gun in the holster, a long blade across the back.</summary>
        static string RideOf(View v, string key, bool pistolOnly) =>
            key == MainCarry && pistolOnly ? SidearmCarry : key == BeltCarry && v.arts[key].slung ? SlungCarry : key;

        /// <summary>`key`'s weapon (`model`) put on its place `mount` on the body as it rides there (`ride`).</summary>
        static void Ride(View v, string key, Transform model, string ride, Transform mount)
        {
            model.SetParent(mount, false);
            model.localRotation = Quaternion.identity;
            float lossy = Mathf.Max(mount.lossyScale.x, 1e-4f);
            model.localScale = Vector3.one * Figures.CharacterScale * Size(v.arts[key]) / lossy;
            // (The gun by its middle, or as far out and down as its art says (WeaponArt.back, F-18); the chainsaw nearer its
            // motor, so that the motor sits in the middle of the back, not at the waist: Gameplay 29/10.)
            var back = ride == MainCarry && v.arts[key].back is float[] b && b.Length >= 2 ? new Vector2(b[0], b[1]) : Vector2.zero;
            model.localPosition = new Vector3(0, back.x / lossy, ride == MainCarry ? (-v.middles[key] - back.y) / lossy : ride == SlungCarry ? -v.middles[key] * SlungBy / lossy : 0);
            // A blade at the belt by its grip, along the way it runs (the pan by its handle, hanging).
            if (ride == BeltCarry) Seat(model, v.arts[key], mount.position, mount.rotation);
            // One across the back whose length runs another way than +z (the rake's, the sword's): turned so, and by a
            // point SlungBy of the way from its grip to its middle along it, as the others by their middle.
            else if (ride == SlungCarry && v.arts[key].along != null)
            {
                var slung = v.arts[key];
                Seat(model, slung, mount.position, mount.rotation);
                var along = new Vector3(slung.along[0], slung.along[1], slung.along[2]).normalized;
                var grip = slung.grip != null && slung.grip.Length >= 3 ? new Vector3(slung.grip[0], slung.grip[1], slung.grip[2]) : Vector3.zero;
                model.position -= mount.forward * (Vector3.Dot(CentreOf(model) - grip, along) * model.lossyScale.x * SlungBy);
            }
        }

        /// <summary>Whether its outfit has a holster of its own with a pistol in it (SoldierMarks.holster: F-08).</summary>
        static bool OwnHolster(View v) => v.body != null && v.body.GetComponent<SoldierMarks>() is SoldierMarks marks && marks.holster;

        /// <summary>
        /// What rides by the hips kept off the body, as it is made (the 3D review's F-07 and F-08, 10/11; 0.10.1's outOfBody
        /// and BELT_ROOM, measured on the body with its build: BodyBulk): the holster pushed out to its right till the pistol
        /// is in it HolsterTouch at most (HolsterStep at a time, HolsterMost in all); the belt out its way (BeltOut) till its
        /// blade is BeltRoom clear (BeltStep, BeltMost). Fixed, they went 10–16 cm into a heavy build, a sweater tied at the
        /// waist, a coat, a bag. The most each goes, less than 0.10.1's (0.2 and 0.3 m): the bulk is the most of each sector
        /// (a bag, a buttock all round), and with those the pistol hung 5–8 cm off the thigh; the belt only out of the bulk,
        /// not BELT_ROOM (2.5 cm) clear of it: with that the knife hung 10 cm off the hip (CombatLab.Trajes). BASTION_SIN_HOLGURA=1,
        /// none (the labs' «before»).
        /// </summary>
        static void KeepOff(View v)
        {
            if (KeepOffOff || v.body == null) return;
            var bulk = BodyBulk.Of(v.animator);
            if (bulk.Empty) return;
            bool pistolOnly = PistolMain(v) && !v.carried.ContainsKey(SidearmCarry);
            foreach (var kv in v.carried)
            {
                string ride = RideOf(v, kv.Key, pistolOnly);
                if ((ride != SidearmCarry && ride != BeltCarry) || !v.stows.TryGetValue(ride, out var mount)) continue;
                Ride(v, kv.Key, kv.Value, ride, mount);
                if (ride == SidearmCarry) bulk.Clear(mount, kv.Value, v.body.right, HolsterStep, HolsterMost, HolsterTouch);
                // (The knife's sheath, if it has one, is what is at the belt: the wider of the two.)
                else bulk.Clear(mount, v.sheath != null ? v.sheath : kv.Value, v.body.TransformDirection(BeltOut), BeltStep, BeltMost, -BeltRoom);
            }
        }

        /// <summary>
        /// The knife's sheath (the 3D review's F-07, 10/11, Arturo's A): POLYGON Military's, put by FigureCatalogMake among
        /// the weapons. At the belt its knife isn't drawn; drawn, the sheath stays there, empty.
        /// </summary>
        public const string KnifeSheath = "military:SM_Chr_Attach_Knife_Sheath_01";

        /// <summary>
        /// BASTION_VAINA=1: the sheath on (the labs) till Arte has looked at it: in CombatLab.Trajes it showed a few
        /// centimetres long off the hip, not the 26 cm hanging down it should (its turn or its size is wrong; not found).
        /// </summary>
        static readonly bool SheathOn = Environment.GetEnvironmentVariable("BASTION_VAINA") == "1";

        /// <summary>The sheath's size against the figures' (its model 38.6 cm long: the knife's 35), and the way it hangs in the body's frame.</summary>
        const float SheathSize = 0.6f;
        static readonly Vector3 SheathAlong = new Vector3(-0.08f, -1, -0.15f).normalized;

        /// <summary>
        /// The knife at its belt in a sheath (F-07): the sheath on the belt's place, its length (its model's longest side)
        /// down the way the blade hung (BeltAlong), its mouth (that side's upper end, as it hangs on a body in its model) where
        /// the knife's grip was, its flat against the hip.
        /// </summary>
        static void Sheathe(View v, FigureCatalog catalog)
        {
            if (!v.arts.TryGetValue(BeltCarry, out var art) || art.slung || art.fbx == null || art.fbx.IndexOf("Knife", StringComparison.OrdinalIgnoreCase) < 0
                || !v.stows.TryGetValue(BeltCarry, out var mount) || catalog?.Weapon(KnifeSheath) is not GameObject prefab) return;
            var sheath = Instantiate(prefab, mount, false).transform;
            sheath.name = "vaina";
            foreach (var c in sheath.GetComponentsInChildren<Collider>()) DestroyNow(c);
            var box = new Bounds();
            bool any = false;
            foreach (var r in sheath.GetComponentsInChildren<Renderer>())
                foreach (var corner in Corners(r.bounds))
                {
                    var p = sheath.InverseTransformPoint(corner);
                    if (!any) box = new Bounds(p, Vector3.zero);
                    else box.Encapsulate(p);
                    any = true;
                }
            if (!any)
            {
                DestroyNow(sheath.gameObject);
                return;
            }
            var size = box.size;
            int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int thin = longest == 0 ? (size.y <= size.z ? 1 : 2) : longest == 1 ? (size.x <= size.z ? 0 : 2) : (size.x <= size.y ? 0 : 1);
            Vector3 Axis(int k) => k == 0 ? Vector3.right : k == 1 ? Vector3.up : Vector3.forward;
            // (Its mouth up: down its length is towards its lower end, by its model's y where its length is upright.)
            var down = longest == 1 ? Vector3.down : -Axis(longest);
            var mouth = box.center - down * size[longest] / 2;
            float lossy = Mathf.Max(mount.lossyScale.x, 1e-4f);
            sheath.localScale = Vector3.one * Figures.CharacterScale * SheathSize / lossy;
            var outward = v.body.TransformDirection(BeltOut);
            // (Down the hip nearly upright, SheathAlong, not back along the blade's BeltAlong: into the buttock that way.)
            sheath.rotation = Quaternion.LookRotation(v.body.TransformDirection(SheathAlong), outward) * Quaternion.Inverse(Quaternion.LookRotation(down, Axis(thin)));
            sheath.position = mount.position - (sheath.TransformPoint(mouth) - sheath.position);
            v.sheath = sheath;
        }

        /// <summary>syntyFigures.ts outOfBody's (the holster) and BELT_OUT's (the belt): steps, the most, how deep or how clear (metres).</summary>
        const float HolsterStep = 0.01f, HolsterMost = 0.06f, HolsterTouch = 0.012f, BeltStep = 0.005f, BeltMost = 0.05f, BeltRoom = 0;
        public static bool KeepOffOff = Environment.GetEnvironmentVariable("BASTION_SIN_HOLGURA") == "1";

        /// <summary>
        /// Dying (the 3D review's F-02 and F-11, 10/11; 0.10.1's syntyFigures.ts dropWeapon): what it carries but the one in
        /// its hands put away (the engine leaves its gear on the floor, MapBuilder.Dropped: the gun on its back went under
        /// the floor and into the nape, the blade out of the hip), and that one let go `after` seconds on (LetGoShare of its
        /// fall). It had stood up out of the dead hand, a pistol pointing at the sky.
        /// </summary>
        static void LetGo(View v, float after)
        {
            if (v.carried.Count == 0) return;
            foreach (var kv in v.carried) if (kv.Key != v.drawn && kv.Value != null) kv.Value.gameObject.SetActive(false);
            if (v.sheath != null) v.sheath.gameObject.SetActive(false);
            v.letGo = Mathf.Max(0, after);
            v.fallen = -1;
        }

        /// <summary>0.10.1's LET_GO (of the fall's seconds) and DROP_SECONDS.</summary>
        const float LetGoShare = 0.3f, DropSeconds = 0.4f;

        /// <summary>
        /// The weapon let go (LetGo) to the floor over DropSeconds (dropWeapon): from where the hand had it, down with u² to
        /// the feet's height and turned onto its side along where it pointed, then lifted out of the floor if any of it is
        /// under. True while it isn't held (HoldNow not to put it back).
        /// </summary>
        bool Drop(View v, float dt)
        {
            if (v.letGo < 0 || v.weapon == null) return false;
            if (v.letGo > 0)
            {
                v.letGo = Mathf.Max(0, v.letGo - dt);
                if (v.letGo > 0) return false;
            }
            if (v.fallen < 0)
            {
                v.weapon.SetParent(v.root, true);
                v.fallFrom = v.weapon.position;
                v.fallTurn = v.weapon.rotation;
                var ahead = Vector3.ProjectOnPlane(v.weapon.forward, Vector3.up);
                if (ahead.sqrMagnitude < 1e-4f) ahead = v.root.forward;
                v.fallTo = Quaternion.LookRotation(ahead.normalized, Vector3.up) * Quaternion.Euler(0, 0, 90);
                v.fallen = 0;
            }
            if (v.fallen > DropSeconds) return true;
            v.fallen = v.fallNow ? DropSeconds : Mathf.Min(DropSeconds, v.fallen + dt);
            float u = v.fallen / DropSeconds;
            var at = v.fallFrom;
            at.y = Mathf.Lerp(v.fallFrom.y, v.root.position.y, u * u);
            v.weapon.SetPositionAndRotation(at, Quaternion.Slerp(v.fallTurn, v.fallTo, u));
            if (u < 1) return true;
            // (Lying: as much of it as is under the floor lifted out, and left there.)
            float low = float.MaxValue;
            foreach (var r in v.weapon.GetComponentsInChildren<Renderer>()) low = Mathf.Min(low, r.bounds.min.y);
            if (low < v.root.position.y + 0.005f && low < float.MaxValue) v.weapon.position += Vector3.up * (v.root.position.y + 0.005f - low);
            v.fallen = DropSeconds + 1;
            HideDropped(v);
            return true;
        }

        /// <summary>
        /// One of the squad's, fallen, whose gear the state has on the floor (GameState.Dropped from it: MapBuilder.Dropped
        /// draws its rucksack with its weapon by it): the weapon it let go goes once it lies, one weapon on the floor (F-02).
        /// </summary>
        void HideDropped(View v)
        {
            if (v.weapon == null || v.fallen <= DropSeconds || state?.Dropped == null) return;
            foreach (var d in state.Dropped)
                if (d.From == v.id)
                {
                    v.weapon.gameObject.SetActive(false);
                    return;
                }
        }

        /// <summary>What it holds now, for how it holds it and which clips: a two-handed gun, the pistol, a blade.</summary>
        enum Held { None, Rifle, Pistol, Blade }

        /// <summary>How it waits with what it holds (syntyFigures.ts direct): a rifle's rest, the pistol by the thigh over the gun arm, a blade none.</summary>
        static void AtRest(View v, float fade)
        {
            var catalog = FigureCatalog.Load();
            if (v.anim == null || catalog == null) return;
            if (v.inHand == Held.Pistol)
            {
                v.anim.Rest(null, fade);
                v.anim.ArmRest(catalog.Clip("pistolRest"), PistolHold, fade);
                return;
            }
            v.anim.ArmRest(null, 0, fade);
            v.anim.Rest(v.inHand == Held.Rifle ? catalog.Clip(RestOf(v)) : null, fade);
        }

        /// <summary>Whether its weapon is up: aimed for a shot, at a target being picked, or on watch; not while it turns to it first (A-03).</summary>
        static bool Up(View v) => !v.turning && (v.aiming || v.picking != null || v.watch);

        /// <summary>On guard with the blade: at a target being picked with it, or on watch with it (Unit.WatchMelee).</summary>
        static bool Guarding(View v) => v.inHand == Held.Blade && (v.picking?.melee == true || v.watch && v.guard && !v.aiming);

        /// <summary>To how it stands with what it holds now: aimed if its weapon is up (Up), on guard with the blade (Guarding), else at rest.</summary>
        static void Settle(View v, float fade)
        {
            if (v.weapon == null) return;
            if (Guarding(v)) Guard(v, fade);
            else if (Up(v) && v.inHand != Held.Blade) Aimed(v, fade);
            else AtRest(v, fade);
        }

        /// <summary>
        /// The blade on guard (unitViews.ts GUARD_AIM, syntyFigures.ts's blade raised part way: the hand up by the chest, the
        /// tip up and ahead): «Stabbing 3» held over the upper body this far in, as it is drawn back to stab, for any blade
        /// (the machete's swing has nothing like it: its arms go wide). Found with CombatLab.Guardia.
        /// </summary>
        const float GuardAt = 0.32f;

        static void Guard(View v, float fade)
        {
            var catalog = FigureCatalog.Load();
            if (v.anim == null || catalog == null) return;
            v.anim.ArmRest(null, 0, fade);
            // A guard by kind (the review of 9/11, Gameplay): a sword at Sword Combat's guard, the long two-handed tools low
            // across the body as a lowered rifle (HoldBlade puts them between the hands); the rest the knife's, drawn back.
            switch (GuardOf(v))
            {
                case "sword":
                    v.anim.Rest(catalog.Clip("bladeIdle"), fade);
                    break;
                case "low":
                    v.anim.Rest(catalog.Clip("rifleLowered"), fade);
                    break;
                default:
                    v.anim.Rest(catalog.Clip(KnifeStroke.clip), fade, GuardAt);
                    break;
            }
        }

        /// <summary>The guard of the blade in its hands: "sword" (the katana, the long sword), "low" (one taken in both hands: the rake, the chainsaw), else the knife's.</summary>
        static string GuardOf(View v)
        {
            var art = v.arts.TryGetValue(v.drawn, out var a) ? a : null;
            if (art == null) return null;
            if (art.two != null && art.two.Length >= 3) return "low";
            string fbx = art.fbx ?? "";
            return fbx.IndexOf("Katana", StringComparison.OrdinalIgnoreCase) >= 0 || fbx.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0 ? "sword" : null;
        }

        /// <summary>
        /// unitViews.ts CLOSE_GAP, CLOSE_BACK: at point blank, its muzzle kept this far short of whoever it aims at, the body
        /// stepped back for it this far at most (tiles); it goes there as the gun comes up.
        /// </summary>
        const float CloseGap = 0.3f, CloseBack = 0.3f;

        /// <summary>
        /// unitViews.ts apply: at point blank it steps back as it shoulders a long gun, its muzzle short of whoever it aims
        /// at (a machine gun reached 0.86 of a tile ahead, into one beside): the body, not the unit's place, moves.
        /// </summary>
        static void StepBack(View v, float dt)
        {
            if (v.body == null || v.dead) return;
            float want = 0;
            // (Before its reach is measured, all the way up, its weapon's own (WeaponArt.reach): stepped back as the gun comes
            // up from the first shot, not after it is up, its muzzle a frame in the target. The 3D review's A-15, 10/11.)
            float reach = v.reachOf == v.drawn && v.reach > 0 ? v.reach : !ReachOff && v.arts.TryGetValue(v.drawn, out var art) ? art.reach : 0;
            if (v.close > 0 && v.inHand == Held.Rifle && reach > 0)
                want = Mathf.Min(CloseBack, Mathf.Max(0, reach - (v.close - CloseGap))) * v.up;
            if (want == v.back) return;
            // (Eased as the gun comes up; the reach is only measured once it is all the way up.)
            v.back = Mathf.MoveTowards(v.back, want, dt * CloseBack / AimBlend);
            v.body.localPosition = new Vector3(0, 0, -v.back * MapSpace.Tile);
        }

        /// <summary>BASTION_SIN_ALCANCE=1: no WeaponArt.reach before the measure (the labs' «before» of the 3D review's A-15).</summary>
        public static bool ReachOff = Environment.GetEnvironmentVariable("BASTION_SIN_ALCANCE") == "1";

        /// <summary>syntyFigures.ts aimReach: how far ahead of its feet the muzzle of the long gun in its hands comes, measured while it is all the way up.</summary>
        static void Reach(View v)
        {
            if (v.inHand != Held.Rifle || v.weapon == null || v.up < 0.99f || v.reload > 0 || v.dead) return;
            var muzzle = v.weapon.TransformPoint(v.muzzle) - v.root.position;
            v.reach = Vector3.Dot(muzzle, v.root.forward) / MapSpace.Tile + v.back;
            v.reachOf = v.drawn;
            if (ReachLog) Debug.Log(FormattableString.Invariant($"[apuntar] reach={v.reach:0.000} {v.id} {(v.arts.TryGetValue(v.drawn, out var art) ? art.fbx : "-")}"));
        }

        /// <summary>BASTION_LOG_ALCANCE=1 (the labs: CombatLab.Apuntar): each reach measured, a line [apuntar] (WeaponArt.reach's numbers; the 3D review's A-15).</summary>
        static readonly bool ReachLog = Environment.GetEnvironmentVariable("BASTION_LOG_ALCANCE") == "1";

        /// <summary>Aimed with what it holds: a rifle's aim (knelt, the kneeling one), the pistol in both hands (pistolAim, syntyFigures.ts direct).</summary>
        static void Aimed(View v, float fade)
        {
            var catalog = FigureCatalog.Load();
            if (v.anim == null || catalog == null) return;
            v.anim.ArmRest(null, 0, fade);
            v.anim.Rest(catalog.Clip(v.inHand == Held.Pistol ? "pistolAim" : v.crouched ? "rifleKneelAim" : "rifleAim"), fade);
        }

        /// <summary>What it holds where the hands hold it, after the body's pose (syntyFigures.ts clipHold): a gun between them, the pistol along the forearm, a blade in the fist.</summary>
        static void HoldNow(View v, float dt = 0)
        {
            if (v.weapon == null || v.animator == null) return;
            switch (v.inHand)
            {
                case Held.Pistol:
                    HoldPistol(v);
                    v.spread.Apply(v.animator, v.anim, v.weapon, dt);
                    break;
                case Held.Blade:
                    HoldBlade(v, dt);
                    break;
                default:
                    // Between the hands as far as the left is on it (twoHands), else rigid in the right as it was last held
                    // there; that turn learnt only with both hands all the way on it (the 3D review's G-1, 10/11). Eased
                    // here, after the frame's clips: a throw's clip comes in at its first step, and a frame late the gun
                    // had learnt its turn from the left hand already gone.
                    bool busy = LeftBusy(v);
                    v.twoHands = Mathf.MoveTowards(v.twoHands, busy ? 0 : 1, dt / (busy ? TwoHandsOff : TwoHandsBack));
                    var before = v.grip;
                    Hold(v.animator, v.weapon, v.root, v.dead || v.twoHands <= 0, ref v.grip, PalmAt(v));
                    if (v.twoHands < 1 && before is Quaternion g && v.animator.GetBoneTransform(HumanBodyBones.RightHand) is Transform hand)
                    {
                        v.weapon.rotation = Quaternion.Slerp(hand.rotation * g, v.weapon.rotation, v.twoHands);
                        v.grip = g;
                    }
                    if (v.inHand == Held.Rifle && v.reload > 0) ReloadTilt(v);
                    break;
            }
        }

        /// <summary>
        /// A long gun reloading no steeper than ReloadTiltMost (the 3D review's F-19, 10/11): «Reload 3» over the rest's turn in
        /// the hand had it upright before the face, half a metre over the head from the game's camera, while the magazine was
        /// out. Turned down ahead about the palm by what is over (the hand stays), the limit coming in and going with the
        /// reload's own ends (ReloadEnds).
        /// </summary>
        static void ReloadTilt(View v)
        {
            var w = v.weapon;
            float up = Mathf.Asin(Mathf.Clamp(w.forward.y, -1, 1)) * Mathf.Rad2Deg, most = Mathf.Lerp(90, ReloadTiltMost, v.reload);
            if (up <= most) return;
            var axis = Vector3.Cross(Vector3.up, w.forward);
            if (axis.sqrMagnitude < 1e-6f) axis = v.root.right;
            w.rotation = Quaternion.AngleAxis(up - most, axis.normalized) * w.rotation;
        }

        /// <summary>
        /// Whether its left hand is off the gun (the 3D review's G-1, 10/11): dead, reloading, searching or treating (from
        /// the frame they begin: their rests change the arms before the pose comes in), or throwing by it
        /// (FigureAnimator.FreeShare: the free hand's clip, which leaves the gun arm be). The decision written in
        /// docs/UNITY-ARTE.md: «se mantiene rígida cuando la izquierda suelta» (0.10.1's clipGrip); the gun had gone after
        /// the left hand wherever its palms were 12–80 cm apart.
        /// </summary>
        static bool LeftBusy(View v) => v.dead || v.reload > 0 || v.search > 0 || v.tended != null || v.searchAt != null || (v.anim != null && v.anim.FreeShare > 0.01f);

        /// <summary>
        /// The point of the long gun in its hands put in the right palm (WeaponArt.palm, its model's frame unscaled; a lab's
        /// own, PalmForLab), as far as the gun isn't up: at rest all of it, aimed none (the 3D review's F-12, 10/11).
        /// </summary>
        static Vector3 PalmAt(View v)
        {
            if (v.inHand != Held.Rifle) return Vector3.zero;
            var at = PalmForLab.TryGetValue(v.id, out var lab) ? lab
                : v.arts.TryGetValue(v.drawn, out var art) && art.palm != null && art.palm.Length >= 3 ? new Vector3(art.palm[0], art.palm[1], art.palm[2]) : Vector3.zero;
            return at * (1 - v.up);
        }

        /// <summary>For the labs (F-12's search): a unit's palm point instead of its weapon's (WeaponArt.palm).</summary>
        public static readonly Dictionary<string, Vector3> PalmForLab = new Dictionary<string, Vector3>();

        /// <summary>Seconds for the gun to go rigid in the right hand as the left lets go, and back between both as it comes back (twoHands).</summary>
        const float TwoHandsOff = 0.1f, TwoHandsBack = 0.2f;

        /// <summary>
        /// The pistol in the right palm, its barrel along the forearm (syntyFigures.ts clipGrip for a pistol: hand − elbow),
        /// that turn learnt in the hand's frame little by little, levelled as it is aimed (forward.y by 1 − aim).
        /// </summary>
        static void HoldPistol(View v)
        {
            if (ConvergeOff) HoldPistol(v.animator, v.weapon, v.root, v.aim, ref v.pistolGrip);
            else HoldPistol(v.animator, v.weapon, v.root, Mathf.Max(v.aim, v.up), ref v.pistolGrip, v.aimAt);
            // A shot's kick: the wrist and the pistol in it flipped up at the muzzle (PistolKick's clip moves the arms; laid on
            // the line to the target, the pistol itself never showed it).
            if (v.pistolKick >= 0 && v.animator.GetBoneTransform(HumanBodyBones.RightHand) is Transform hand)
            {
                float k = v.pistolKick, flip = k < FlipRise ? k / FlipRise : Mathf.Max(0, 1 - (k - FlipRise) / FlipFall);
                if (flip <= 0) return;
                var right = Vector3.Cross(Vector3.up, v.weapon.forward);
                if (right.sqrMagnitude < 1e-6f) return;
                var q = Quaternion.AngleAxis(-FlipDegrees * Mathf.SmoothStep(0, 1, flip), right.normalized);
                var wrist = hand.position;
                hand.rotation = q * hand.rotation;
                v.weapon.SetPositionAndRotation(wrist + q * (v.weapon.position - wrist), q * v.weapon.rotation);
            }
        }

        /// <summary>The pistol's kick at the muzzle (HoldPistol): this far up (degrees), there in FlipRise and back over FlipFall (seconds).</summary>
        const float FlipDegrees = 10, FlipRise = 0.06f, FlipFall = 0.3f;

        /// <summary>HoldPistol(View) for any figure: `a`'s right hand on `pistol`, `root` its upright, aimed `aim` (0–1), `grip` its turn learnt in the hand.</summary>
        public static void HoldPistol(Animator a, Transform pistol, Transform root, float aim, ref Quaternion? grip) => HoldPistol(a, pistol, root, aim, ref grip, null);

        /// <summary>
        /// The same, aimed at `at` (the review of 9/11): laid on the line from the palm to it as it is aimed, not level
        /// (a target on another storey; knelt, its forearm had it 23° over the line).
        /// </summary>
        static void HoldPistol(Animator a, Transform pistol, Transform root, float aim, ref Quaternion? grip, Vector3? at)
        {
            if (a == null || pistol == null) return;
            Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand), elbow = a.GetBoneTransform(HumanBodyBones.RightLowerArm), knuckle = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (hand == null || elbow == null) return;
            // (The hand closed on its grip, the index along the trigger guard: 0.10.1's `trigger` pose. «Pistol Aim» held at
            // rest and Synty's runs left it open, the fingers hanging 5–8 cm off the grip: the 3D review's P-3, 10/11.)
            Fist(a, TriggerIndex, TriggerOthers);
            var palm = knuckle != null ? Vector3.Lerp(hand.position, knuckle.position, 0.6f) : hand.position;
            var forearm = (hand.position - elbow.position).normalized;
            var up = Vector3.ProjectOnPlane(root.up, forearm);
            if (up.sqrMagnitude < 0.04f) up = Vector3.ProjectOnPlane(root.forward, forearm);
            var want = Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(forearm, up);
            grip = grip.HasValue ? Quaternion.Slerp(grip.Value, want, 0.2f) : want;
            var turn = hand.rotation * grip.Value;
            if (aim > 0)
            {
                var ahead = turn * Vector3.forward;
                var level = at is Vector3 p && (p - palm).sqrMagnitude > 1e-4f ? Vector3.Slerp(ahead, (p - palm).normalized, aim) : new Vector3(ahead.x, ahead.y * (1 - aim), ahead.z);
                if (level.sqrMagnitude > 1e-6f) turn = Quaternion.LookRotation(level, Vector3.ProjectOnPlane(Vector3.up, level));
            }
            pistol.SetPositionAndRotation(palm, turn);
        }

        /// <summary>
        /// The support hand on the handguard (the review of 9/11): the gun lies along the line from the right palm to the
        /// left (Hold), wherever the clips put the left one, so on a short gun the left hand held the muzzle (the
        /// submachine gun's 20 cm past its handguard) and on most a hand's breadth ahead of it (CombatLab.Agarres). The left
        /// arm reaches its palm to the gun's `fore` (WeaponArt), the elbow where the clip has it, once the gun is placed
        /// and aimed; only with both hands on it, not reloading, and not from further than SupportMost.
        /// </summary>
        static void Support(View v, float dt = 0)
        {
            // (Out of reach, or the left hand busy, it begins again from nothing as it comes back within SupportMost.)
            float share = v.supportShare;
            v.supportShare = 0;
            if (SupportOff || v.inHand != Held.Rifle || v.weapon == null || v.animator == null || !v.animator.isHuman || v.dead || v.twoHands <= 0) return;
            var art = v.arts.TryGetValue(v.drawn, out var x) ? x : null;
            if (art?.fore == null || art.fore.Length < 2) return;
            var a = v.animator;
            Transform shoulder = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), elbow = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform knuckle = a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal), right = a.GetBoneTransform(HumanBodyBones.RightHand), rightKnuckle = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (shoulder == null || elbow == null || hand == null || knuckle == null || right == null) return;
            Vector3 Palm() => Vector3.Lerp(hand.position, knuckle.position, 0.6f);
            var rightPalm = rightKnuckle != null ? Vector3.Lerp(right.position, rightKnuckle.position, 0.6f) : right.position;
            float apart = Vector3.Distance(rightPalm, Palm()) / Figures.CharacterScale;
            if (apart <= 0.12f || apart >= 0.8f) return;
            var fore = v.weapon.TransformPoint(new Vector3(0, art.fore[0], art.fore[1]));
            if ((fore - Palm()).sqrMagnitude > SupportMost * SupportMost) return;
            // (As far as the gun is back between both hands, twoHands, and over TwoHandsBack from when the palm comes within
            // SupportMost: the left palm eased onto the handguard, not there in a frame as a reload, a throw or a blow ends;
            // the 3D review's G-1, 10/11. Held steadily, all of it, as before.)
            v.supportShare = Mathf.MoveTowards(share, 1, dt / TwoHandsBack);
            var goal = Vector3.Lerp(Palm(), fore, v.twoHands * v.supportShare);
            // (Twice: the palm is ahead of the wrist, and it turns with the forearm.)
            for (int pass = 0; pass < 2; pass++)
                Reach(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, hand.position + (goal - Palm()), elbow.position - shoulder.position, 0.005f);
        }

        /// <summary>How far Support moves the left palm at most (metres), and BASTION_SIN_MANO=1, none at all (the labs' «before»).</summary>
        const float SupportMost = 0.35f;
        public static bool SupportOff = Environment.GetEnvironmentVariable("BASTION_SIN_MANO") == "1";

        /// <summary>
        /// A blade in the right fist as the hand holds it (syntyFigures.ts clipHold with no clipGrip, 0.10.1's weaponLab
        /// knife and machete): its blade on along the fingers (down by the leg with the arm hanging, out along the arm as it
        /// swings), its back towards the index's side.
        /// </summary>
        static void HoldBlade(View v, float dt = 0)
        {
            var a = v.animator;
            Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand), index = a.GetBoneTransform(HumanBodyBones.RightIndexProximal),
                little = a.GetBoneTransform(HumanBodyBones.RightLittleProximal), middle = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (hand == null || index == null || little == null || middle == null) return;
            Fist(a);
            var palm = Vector3.Lerp(hand.position, middle.position, 0.6f);
            var fingers = (middle.position - hand.position).normalized;
            var across = Vector3.ProjectOnPlane(index.position - little.position, fingers);
            var art = v.arts.TryGetValue(v.drawn, out var a2) ? a2 : null;
            // On guard low across the body (the rake, the chainsaw: GuardOf "low"), held as a rifle between the hands: its
            // grip in the right palm, its second grip towards the left (the left hand reached onto it below).
            var leftHand = a.GetBoneTransform(HumanBodyBones.LeftHand);
            var leftKnuckle = a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            // (The chainsaw on guard ahead of it, not across: its back handle in the right palm, its bar on along the way it
            // faces, ChainsawDown under the level; the left hand onto its front handle below. Across the body as a lowered
            // rifle, from one handle to the other, its bar stood out to a side or behind: the 3D review's A-24 b2, 10/11, with
            // Gameplay's yes if it reads better than that.)
            if (Guarding(v) && GuardOf(v) == "low" && art?.fbx != null && art.fbx.IndexOf("ChainSaw", StringComparison.OrdinalIgnoreCase) >= 0)
                Seat(v.weapon, art, palm, Quaternion.LookRotation(Quaternion.AngleAxis(ChainsawDown, v.root.right) * v.root.forward, v.root.up));
            else if (Guarding(v) && GuardOf(v) == "low" && leftHand != null && leftKnuckle != null && art?.grip != null && art.grip.Length >= 3)
            {
                // (The right hand LowAhead further out, the weapon turned on to the left one: at «Rifle Lowered»'s hands the
                // chainsaw's body was 12–14 cm into the belly, CombatLab.Cuerpo. The left hand onto its other grip below.)
                var push = v.root.forward * LowAhead * (v.body != null ? v.body.lossyScale.x : 1);
                Reach(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, hand.position + push,
                    v.root.TransformDirection(new Vector3(1, -0.7f, 0)), 0.02f);
                palm = Vector3.Lerp(hand.position, middle.position, 0.6f);
                var leftPalm = Vector3.Lerp(leftHand.position, leftKnuckle.position, 0.6f);
                var grip = new Vector3(art.grip[0], art.grip[1], art.grip[2]);
                var two = new Vector3(art.two[0], art.two[1], art.two[2]);
                var dirModel = (two - grip).normalized;
                var upModel = Mathf.Abs(dirModel.z) < 0.9f ? Vector3.forward : Vector3.up;
                var dirWorld = leftPalm - palm;
                if (dirWorld.sqrMagnitude > 1e-6f && dirModel.sqrMagnitude > 1e-6f)
                {
                    var turn = Quaternion.LookRotation(dirWorld.normalized, Vector3.ProjectOnPlane(v.root.forward, dirWorld)) * Quaternion.Inverse(Quaternion.LookRotation(dirModel, upModel));
                    v.weapon.rotation = turn;
                    v.weapon.position = palm - (v.weapon.TransformPoint(grip) - v.weapon.position);
                }
            }
            else
            {
                // (Moving, neither on guard nor striking: carried out past the leg and no steeper than CarryUpMost, eased in
                // and out over CarryEase. The 3D review's H-1, 10/11: sprinting, the bat hung straight from the fist into the
                // shin, and swung up it stood before the face.)
                // (One-handed only: the rake, between both hands, went 10 cm into the trunk turned so.)
                v.carry = Mathf.MoveTowards(v.carry, v.walking && !v.striking && !Guarding(v) && art?.two == null ? 1 : 0, dt / CarryEase);
                if (v.carry > 0) fingers = Carried(v, fingers);
                // (On a sword's guard, out of the fist on the thumb's side, up and ahead as Sword Combat's guard holds it, its
                // edge towards the knuckles: along the fingers, with the hand hanging back, its point was on the floor behind.
                // The 3D review's A-24 b1, 10/11.)
                if (Guarding(v) && GuardOf(v) == "sword" && across.sqrMagnitude > 1e-6f) Seat(v.weapon, art, palm, Quaternion.LookRotation(across, -fingers));
                else Seat(v.weapon, art, palm, Quaternion.LookRotation(fingers, across.sqrMagnitude > 1e-6f ? across : v.root.up));
            }
            // Two-handed (the chainsaw): the left hand on its other grip, the elbow out to the left and down.
            if (art?.two != null && art.two.Length >= 3)
                Reach(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, v.weapon.TransformPoint(new Vector3(art.two[0], art.two[1], art.two[2])),
                    v.root.TransformDirection(new Vector3(-1, -0.7f, 0)), 0.02f);
        }

        /// <summary>How far out a low guard holds its weapon's grip past «Rifle Lowered»'s right hand (metres, of a figure's scale 1).</summary>
        const float LowAhead = 0.13f;
        /// <summary>How far under the level the chainsaw's bar points on guard (degrees; A-24 b2).</summary>
        const float ChainsawDown = 25;

        /// <summary>
        /// H-1: a blade carried moving turned out to its right by CarryOut (degrees, round its forward) and down to CarryUpMost
        /// over the level at most, as far as it is carried (View.carry, eased over CarryEase seconds).
        /// </summary>
        const float CarryOut = 25, CarryUpMost = 30, CarryEase = 0.2f;

        static Vector3 Carried(View v, Vector3 fingers)
        {
            // (Out: of the two ways round its forward, the one that takes it to its right.)
            var outA = Quaternion.AngleAxis(CarryOut * v.carry, v.root.forward) * fingers;
            var outB = Quaternion.AngleAxis(-CarryOut * v.carry, v.root.forward) * fingers;
            var turned = Vector3.Dot(outA - outB, v.root.right) >= 0 ? outA : outB;
            float up = Mathf.Asin(Mathf.Clamp(turned.y, -1, 1)) * Mathf.Rad2Deg, most = Mathf.Lerp(90, CarryUpMost, v.carry);
            if (up > most)
            {
                // (Down ahead, as ReloadTilt turns a gun.)
                var axis = Vector3.Cross(Vector3.up, turned);
                if (axis.sqrMagnitude > 1e-6f) turned = Quaternion.AngleAxis(up - most, axis.normalized) * turned;
            }
            return turned;
        }

        /// <summary>
        /// The right hand closed round what it holds (the review of 9/11: Mixamo's strokes are a bare hand's, open, and the
        /// blade lay under the palm): each finger bent at its first two joints to FistBend at least towards the palm,
        /// never opened where the clip closes it further; the thumb over them (its last two bones towards the index's middle
        /// joint, ThumbMost at most: the strokes held it up).
        /// </summary>
        static void Fist(Animator a) => Fist(a, FistBend, FistBend);

        /// <summary>
        /// The left hand closed as a fist (the camp's folded arms and leaning stances, Settlement's CampFigure: the 3D review's
        /// A-31, point 3, 10/11, its fingers open and stiff tucked under the arm), or the right one.
        /// </summary>
        internal static void Fist(Animator a, bool left) => Fist(a, FistBend, FistBend, left);

        /// <summary>The same, the index bent `forefinger` (its first joint and its second, degrees) and the other three `others`; `left`, the left hand.</summary>
        static void Fist(Animator a, Vector2 forefinger, Vector2 others, bool left = false)
        {
            HumanBodyBones B(HumanBodyBones right, HumanBodyBones leftOne) => left ? leftOne : right;
            Transform hand = a.GetBoneTransform(B(HumanBodyBones.RightHand, HumanBodyBones.LeftHand)), index = a.GetBoneTransform(B(HumanBodyBones.RightIndexProximal, HumanBodyBones.LeftIndexProximal)),
                little = a.GetBoneTransform(B(HumanBodyBones.RightLittleProximal, HumanBodyBones.LeftLittleProximal)), middle = a.GetBoneTransform(B(HumanBodyBones.RightMiddleProximal, HumanBodyBones.LeftMiddleProximal));
            if (hand == null || index == null || little == null || middle == null) return;
            // (The palm's side: the fingers' line crossed with the knuckles' from the little finger to the index; the other
            // way round on the left, a mirror's.)
            var palmward = Vector3.Cross(middle.position - hand.position, index.position - little.position).normalized * (left ? -1 : 1);
            foreach (var (p, m, d) in new[]
            {
                (B(HumanBodyBones.RightIndexProximal, HumanBodyBones.LeftIndexProximal), B(HumanBodyBones.RightIndexIntermediate, HumanBodyBones.LeftIndexIntermediate), B(HumanBodyBones.RightIndexDistal, HumanBodyBones.LeftIndexDistal)),
                (B(HumanBodyBones.RightMiddleProximal, HumanBodyBones.LeftMiddleProximal), B(HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.LeftMiddleIntermediate), B(HumanBodyBones.RightMiddleDistal, HumanBodyBones.LeftMiddleDistal)),
                (B(HumanBodyBones.RightRingProximal, HumanBodyBones.LeftRingProximal), B(HumanBodyBones.RightRingIntermediate, HumanBodyBones.LeftRingIntermediate), B(HumanBodyBones.RightRingDistal, HumanBodyBones.LeftRingDistal)),
                (B(HumanBodyBones.RightLittleProximal, HumanBodyBones.LeftLittleProximal), B(HumanBodyBones.RightLittleIntermediate, HumanBodyBones.LeftLittleIntermediate), B(HumanBodyBones.RightLittleDistal, HumanBodyBones.LeftLittleDistal)),
            })
            {
                Transform prox = a.GetBoneTransform(p), mid = a.GetBoneTransform(m), dist = a.GetBoneTransform(d);
                if (prox == null || mid == null) continue;
                var bend = p == B(HumanBodyBones.RightIndexProximal, HumanBodyBones.LeftIndexProximal) ? forefinger : others;
                Bend(prox, mid, (prox.position - hand.position).normalized, palmward, bend.x);
                if (dist != null) Bend(mid, dist, (mid.position - prox.position).normalized, palmward, bend.y);
            }
            Transform thumb = a.GetBoneTransform(B(HumanBodyBones.RightThumbIntermediate, HumanBodyBones.LeftThumbIntermediate)), thumbTip = a.GetBoneTransform(B(HumanBodyBones.RightThumbDistal, HumanBodyBones.LeftThumbDistal)),
                over = a.GetBoneTransform(B(HumanBodyBones.RightIndexIntermediate, HumanBodyBones.LeftIndexIntermediate));
            if (thumb != null && thumbTip != null && over != null) Aim(thumb, thumbTip, over.position, ThumbMost);
        }

        /// <summary>How far Fist turns the thumb at most (degrees).</summary>
        const float ThumbMost = 60;

        /// <summary>`bone` turned so that the line to `child` points at `at`, by `most` degrees at most.</summary>
        static void Aim(Transform bone, Transform child, Vector3 at, float most)
        {
            Vector3 d = child.position - bone.position, want = at - bone.position;
            if (d.sqrMagnitude < 1e-8f || want.sqrMagnitude < 1e-8f) return;
            var turn = Quaternion.FromToRotation(d, want);
            turn.ToAngleAxis(out float angle, out var axis);
            if (angle > 180) angle -= 360;
            bone.rotation = Quaternion.AngleAxis(Mathf.Clamp(angle, -most, most), axis) * bone.rotation;
        }

        /// <summary>How far a fist bends each finger at its first joint and its second (degrees).</summary>
        static readonly Vector2 FistBend = new Vector2(75, 85);
        /// <summary>
        /// A hand on a pistol's grip (P-3): 0.10.1's `trigger` pose (syntyFigures.ts), the index 0.45 and 0.35 rad, the other
        /// three 1.2 and 1.35, in degrees.
        /// </summary>
        static readonly Vector2 TriggerIndex = new Vector2(26, 20), TriggerOthers = new Vector2(69, 77);

        /// <summary>`bone` turned so that the line to `child` bends from `before` towards `palmward` by `want` degrees, if it bends less.</summary>
        static void Bend(Transform bone, Transform child, Vector3 before, Vector3 palmward, float want)
        {
            var d = child.position - bone.position;
            if (d.sqrMagnitude < 1e-8f) return;
            d.Normalize();
            var axis = Vector3.Cross(before, palmward);
            if (axis.sqrMagnitude < 1e-8f) return;
            axis.Normalize();
            float now = Vector3.SignedAngle(before, d, axis);
            // (Which way round the axis takes the line towards the palm.)
            float toward = Vector3.Dot(Quaternion.AngleAxis(10, axis) * before, palmward) > Vector3.Dot(before, palmward) ? 1 : -1;
            float bent = now * toward;
            if (bent >= want) return;
            bone.rotation = Quaternion.AngleAxis((want - bent) * toward, axis) * bone.rotation;
        }

        /// <summary>
        /// `model` (`art`'s) with its grip at `at` and the way it runs from there along `turn`'s forward, its top (+y) towards
        /// `turn`'s up (WeaponArt grip and along; without them, as the knife and the machete: its origin, +z).
        /// </summary>
        static void Seat(Transform model, WeaponArt art, Vector3 at, Quaternion turn)
        {
            var along = art?.along != null && art.along.Length >= 3 ? new Vector3(art.along[0], art.along[1], art.along[2]) : Vector3.forward;
            model.rotation = turn * Quaternion.Inverse(Quaternion.LookRotation(along, Mathf.Abs(along.normalized.y) > 0.9f ? Vector3.forward : Vector3.up));
            var grip = art?.grip != null && art.grip.Length >= 3 ? new Vector3(art.grip[0], art.grip[1], art.grip[2]) : Vector3.zero;
            model.position = at - (model.TransformPoint(grip) - model.position);
        }

        /// <summary>For the labs: where each of `id`'s weapons is (its carry, what holds it, its place and height over the feet, metres), which is drawn.</summary>
        public string ArmsReport(string id)
        {
            if (!views.TryGetValue(id, out var v)) return "-";
            var feet = v.root.position;
            return $"{v.drawn} drawn ({v.inHand}, the arm out {v.spread.Out:0.000}); " + string.Join("; ", v.carried.Select(kv =>
                $"{kv.Key} {v.arts[kv.Key].fbx} on {kv.Value.parent?.name} at {(kv.Value.position - feet):F2}, scale {kv.Value.lossyScale.x:0.###}, {(kv.Value.gameObject.activeInHierarchy ? "shown" : "hidden")}"))
                + "; mounts " + string.Join(", ", v.stows.Select(kv => $"{kv.Key} on {kv.Value.parent?.name} at {(kv.Value.position - feet):F2} (scale {kv.Value.lossyScale.x:0.###})"));
        }

        /// <summary>For the labs: `id`'s blade drawn and its stroke's clip held at `at` seconds (where the guard is looked for).</summary>
        public void GuardForLab(string id, float at)
        {
            if (!views.TryGetValue(id, out var v) || v.anim == null) return;
            Draw(v, BeltCarry);
            var stroke = StrokeOf(v);
            v.anim.ArmRest(null, 0, 0);
            v.anim.Rest(FigureCatalog.Load()?.Clip(stroke.clip), 0, at);
        }

        /// <summary>For the labs (CombatLab.Agarres, the review of 9/11): the weapon each unit holds as its main one whatever its arms (Anims.json's weapons by id), nothing else carried.</summary>
        public static readonly Dictionary<string, string> ArtsForLab = new Dictionary<string, string>();

        /// <summary>For the labs: `id` waiting at rest as `rest` (one of RIFLE_RESTS), whatever its seed gave.</summary>
        public void RestForLab(string id, string rest)
        {
            if (!views.TryGetValue(id, out var v)) return;
            v.rest = rest;
            Settle(v, 0.2f);
        }

        /// <summary>For the labs: where the muzzle of what `id` holds is in the model's own frame.</summary>
        public Vector3 MuzzleLocalForLab(string id) => views.TryGetValue(id, out var v) ? v.muzzle : Vector3.zero;

        /// <summary>For the labs: what `id` carries and shows, each model with whether it is the one in its hands.</summary>
        public List<(Transform model, bool drawn)> CarriedForLab(string id) =>
            views.TryGetValue(id, out var v) ? v.carried.Where(kv => kv.Value != null && kv.Value.gameObject.activeInHierarchy).Select(kv => (kv.Value, kv.Key == v.drawn))
                .Concat(v.sheath != null && v.sheath.gameObject.activeInHierarchy ? new[] { (v.sheath, false) } : Array.Empty<(Transform, bool)>()).ToList() : new List<(Transform, bool)>();

        /// <summary>
        /// For the labs (the review of 9/11): how the hands hold what is drawn, in centimetres in the world: the left palm
        /// along the barrel from the grip (Lz) and off its line (Loff), the muzzle's place along it, the left palm from the
        /// handguard (WeaponArt.fore: [height, length] in the gun's frame) and from a blade's second grip (two), and how
        /// far apart the palms are at the model's scale (Hold takes both hands on it from 12 to 80).
        /// </summary>
        public string GripReport(string id)
        {
            if (!views.TryGetValue(id, out var v) || v.weapon == null || v.animator == null) return "-";
            var a = v.animator;
            Vector3? Palm(HumanBodyBones hand, HumanBodyBones knuckle) =>
                a.GetBoneTransform(hand) is Transform h ? a.GetBoneTransform(knuckle) is Transform k ? Vector3.Lerp(h.position, k.position, 0.6f) : h.position : (Vector3?)null;
            var right = Palm(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal);
            var left = Palm(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal);
            var art = v.arts.TryGetValue(v.drawn, out var x) ? x : null;
            var w = v.weapon;
            string Cm(float m) => (m * 100).ToString("0.0", CultureInfo.InvariantCulture);
            string Deg(float d) => d.ToString("0.0", CultureInfo.InvariantCulture);
            var parts = new List<string> { $"held={v.inHand}" };
            // (The 3D review of 10/11: a long gun's turn in the right hand against the one learnt there, G-1's «rigid»; its
            // barrel's rise over the level, F-19's «up»; the muzzle's height over the feet, A-07's «muzzleY».)
            if (v.inHand == Held.Rifle)
            {
                if (v.grip is Quaternion g && a.GetBoneTransform(HumanBodyBones.RightHand) is Transform rh) parts.Add($"rigid={Deg(Quaternion.Angle(Quaternion.Inverse(rh.rotation) * w.rotation, g))}");
                parts.Add($"up={Deg(Mathf.Asin(Mathf.Clamp(w.forward.y, -1, 1)) * Mathf.Rad2Deg)}");
            }
            parts.Add($"muzzleY={Cm(w.TransformPoint(v.muzzle).y - v.root.position.y)}");
            // (A blade's tip from the hips in the root's frame, ahead and up: the 3D review's A-24, 10/11.)
            if (v.inHand == Held.Blade && BladeLine(v) is var (_, tip) && a.GetBoneTransform(HumanBodyBones.Hips) is Transform hips)
            {
                var d = tip - hips.position;
                parts.Add(FormattableString.Invariant($"tip={Vector3.Dot(d, v.root.forward):0.00},{d.y:0.00}"));
            }
            if (left is Vector3 l)
            {
                // (The left palm from the middle of the magazine of what it holds: the 3D review's P-1, 10/11.)
                if (MagazineOf(w) is Transform mag && mag.GetComponent<Renderer>() is Renderer mr) parts.Add($"Lmag={Cm(Vector3.Distance(l, mr.bounds.center))}");
                var local = w.InverseTransformPoint(l);
                float scale = w.lossyScale.z;
                parts.Add($"Lz={Cm(local.z * scale)}");
                parts.Add($"Loff={Cm(new Vector2(local.x, local.y).magnitude * scale)}");
                parts.Add($"muzzleZ={Cm(v.muzzle.z * scale)}");
                if (art?.fore != null && art.fore.Length >= 2) parts.Add($"Lfore={Cm(Vector3.Distance(l, w.TransformPoint(new Vector3(0, art.fore[0], art.fore[1]))))}");
                if (art?.two != null && art.two.Length >= 3) parts.Add($"Ltwo={Cm(Vector3.Distance(l, w.TransformPoint(new Vector3(art.two[0], art.two[1], art.two[2]))))}");
                if (right is Vector3 r) parts.Add($"apart={Cm(Vector3.Distance(r, l) / Figures.CharacterScale)}");
            }
            return string.Join(" ", parts);
        }

        /// <summary>
        /// The magazine of a gun (a part of its own, Apocalypse's «…_Magazine_01», Military's pistol's «SM_Wep_Pistol_Mag_01»),
        /// or null.
        /// </summary>
        static Transform MagazineOf(Transform gun) =>
            gun != null ? gun.GetComponentsInChildren<Transform>().FirstOrDefault(m => m != gun && (m.name.Contains("Magazine") || m.name.Contains("_Mag_"))) : null;

        /// <summary>
        /// The blade in its hand as a line: from its grip (WeaponArt.grip) to its tip (the farthest of its box along the way it
        /// runs, WeaponArt.along), in the world; null for anything else.
        /// </summary>
        static (Vector3 grip, Vector3 tip)? BladeLine(View v)
        {
            if (v.inHand != Held.Blade || v.weapon == null || !v.arts.TryGetValue(v.drawn, out var art)) return null;
            var along = art.along != null && art.along.Length >= 3 ? new Vector3(art.along[0], art.along[1], art.along[2]).normalized : Vector3.forward;
            var grip = art.grip != null && art.grip.Length >= 3 ? new Vector3(art.grip[0], art.grip[1], art.grip[2]) : Vector3.zero;
            float far = 0;
            foreach (var r in v.weapon.GetComponentsInChildren<Renderer>())
                foreach (var corner in Corners(r.bounds)) far = Mathf.Max(far, Vector3.Dot(v.weapon.InverseTransformPoint(corner) - grip, along));
            return (v.weapon.TransformPoint(grip), v.weapon.TransformPoint(grip + along * far));
        }

        /// <summary>For the labs: the blade in `id`'s hand as a line, grip to tip, in the world (BladeLine), or null.</summary>
        public (Vector3 grip, Vector3 tip)? BladeForLab(string id) => views.TryGetValue(id, out var v) ? BladeLine(v) : null;

        /// <summary>For the labs: where each one's eyes look in its head's frame, taken at rest looking straight ahead (CalibrateGazeForLab).</summary>
        static readonly Dictionary<string, Vector3> GazeForLab = new Dictionary<string, Vector3>();

        /// <summary>For the labs: `id`'s eyes taken to look along its root's forward as it stands now (at rest, level), for AimReport.</summary>
        public string CalibrateGazeForLab(string id)
        {
            if (!views.TryGetValue(id, out var v) || v.animator == null || !(v.animator.GetBoneTransform(HumanBodyBones.Head) is Transform head)) return "-";
            GazeForLab[id] = Quaternion.Inverse(head.rotation) * v.root.forward;
            // (Against the bind pose's, Look's: how far apart, looked at from the head as it stands now.)
            var bind = Gaze(v, head);
            return bind == Vector3.zero ? "no bind pose" : $"the bind pose's {Vector3.Angle(head.rotation * bind, v.root.forward):0.0}° off ahead (yaw {Vector3.SignedAngle(new Vector3(v.root.forward.x, 0, v.root.forward.z), new Vector3((head.rotation * bind).x, 0, (head.rotation * bind).z), Vector3.up):0.0})";
        }

        /// <summary>
        /// For the labs (the review of 9/11): where `id`'s weapon points against `at` (degrees: its barrel's line, +z, from
        /// the line from its muzzle to `at`, round the upright and up and down; and how far that line passes from `at`, in
        /// centimetres), and its eyes' (from between them, along the head's own line of sight as its bind pose has it:
        /// Gaze) against the line from them to `at`.
        /// </summary>
        public string AimReport(string id, Vector3 at)
        {
            if (!views.TryGetValue(id, out var v) || v.animator == null) return "-";
            string F(float f) => f.ToString("0.0", CultureInfo.InvariantCulture);
            float Pitch(Vector3 d) => Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            float Yaw(Vector3 from, Vector3 to) => Vector3.SignedAngle(new Vector3(from.x, 0, from.z), new Vector3(to.x, 0, to.z), Vector3.up);
            var parts = new List<string>();
            if (v.weapon != null)
            {
                var muzzle = v.weapon.TransformPoint(v.muzzle);
                var barrel = v.weapon.forward;
                var want = at - muzzle;
                float miss = Vector3.Cross(barrel, want).magnitude;
                if (Vector3.Dot(barrel, want) < 0) miss = want.magnitude;
                parts.Add($"barrelYaw={F(Yaw(want, barrel))} barrelPitch={F(Pitch(barrel) - Pitch(want))} miss={F(miss * 100)}");
                // (A rifle's butt against the right shoulder's joint: how far, and how far under it; the eyes over the bore's
                // line where they are along it, and to its side: a cheek on the stock.)
                if (v.inHand == Held.Rifle && v.animator.GetBoneTransform(HumanBodyBones.RightUpperArm) is Transform shoulder)
                {
                    var butt = v.weapon.TransformPoint(v.butt);
                    parts.Add($"butt={F((butt - shoulder.position).magnitude * 100)} buttDown={F((shoulder.position.y - butt.y) * 100)}");
                    Transform l = v.animator.GetBoneTransform(HumanBodyBones.LeftEye), r = v.animator.GetBoneTransform(HumanBodyBones.RightEye);
                    if (l != null && r != null)
                    {
                        var local = v.weapon.InverseTransformPoint((l.position + r.position) / 2) * v.weapon.lossyScale.z;
                        parts.Add($"eyesOver={F(local.y * 100)} eyesSide={F(local.x * 100)}");
                    }
                }
            }
            var a = v.animator;
            Transform le = a.GetBoneTransform(HumanBodyBones.LeftEye), re = a.GetBoneTransform(HumanBodyBones.RightEye), head = a.GetBoneTransform(HumanBodyBones.Head);
            if (le != null && re != null && head != null && Gaze(v, head) is Vector3 gaze && gaze != Vector3.zero)
            {
                var mid = (le.position + re.position) / 2;
                var eyes = head.rotation * gaze;
                var want = at - mid;
                parts.Add($"eyesYaw={F(Yaw(want, eyes))} eyesPitch={F(Pitch(eyes) - Pitch(want))}");
            }
            parts.Add($"rootYaw={F(Yaw(at - v.root.position, v.root.forward))}");
            // (How far its weapon is up: the 3D review's A-03, the gun raised once the turn is done.)
            parts.Add($"up={v.up.ToString("0.00", CultureInfo.InvariantCulture)}");
            return string.Join(" ", parts);
        }

        /// <summary>
        /// For the labs (0.12.13, the rifle shouldered): in the trunk's frame (x out to the right, from the left shoulder's
        /// joint to the right's; y up the spine; z ahead), centimetres, the butt from the right shoulder's joint; the right
        /// eye against the bore (its line through the muzzle along the barrel): how far over it and to its right, and how
        /// far behind the butt along it; the sights' height over the bore (WeaponArt.sight, scaled); the face's look against
        /// the barrel (degrees: round the upright and up, Gaze); and the hips' drop from where the legs stand straight.
        /// </summary>
        public string ShoulderReport(string id)
        {
            if (!views.TryGetValue(id, out var v) || v.animator == null || v.weapon == null || v.inHand != Held.Rifle) return "-";
            string F(float f) => f.ToString("0.0", CultureInfo.InvariantCulture);
            var a = v.animator;
            Transform rs = a.GetBoneTransform(HumanBodyBones.RightUpperArm), ls = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), hips = a.GetBoneTransform(HumanBodyBones.Hips),
                neck = a.GetBoneTransform(HumanBodyBones.Neck), eye = a.GetBoneTransform(HumanBodyBones.RightEye), head = a.GetBoneTransform(HumanBodyBones.Head);
            if (rs == null || ls == null || hips == null || neck == null || eye == null || head == null) return "-";
            var x = (rs.position - ls.position).normalized;
            var y = Vector3.ProjectOnPlane(neck.position - hips.position, x).normalized;
            var z = Vector3.Cross(x, y);
            Vector3 Trunk(Vector3 d) => new Vector3(Vector3.Dot(d, x), Vector3.Dot(d, y), Vector3.Dot(d, z)) * 100;
            var w = v.weapon;
            float scale = w.lossyScale.z;
            var butt = w.TransformPoint(v.butt);
            var b = Trunk(butt - rs.position);
            var e = w.InverseTransformPoint(eye.position);
            float over = (e.y - v.muzzle.y) * scale, side = (e.x - v.muzzle.x) * scale, behind = (v.butt.z - e.z) * scale;
            var art = v.arts.TryGetValue(v.drawn, out var aa) ? aa : null;
            var parts = new List<string> { $"butt=({F(b.x)},{F(b.y)},{F(b.z)})", $"eyeOver={F(over * 100)} eyeSide={F(side * 100)} eyeBehind={F(behind * 100)}", $"sight={F((art?.sight ?? 0) * scale * 100)}" };
            if (v.gaze == Vector3.zero) v.gaze = Gaze(v, head);
            if (v.gaze != Vector3.zero)
            {
                var look = head.rotation * v.gaze;
                var barrel = w.forward;
                float yaw = Vector3.SignedAngle(new Vector3(barrel.x, 0, barrel.z), new Vector3(look.x, 0, look.z), Vector3.up);
                float pitch = (Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) - Mathf.Atan2(barrel.y, new Vector2(barrel.x, barrel.z).magnitude)) * Mathf.Rad2Deg;
                parts.Add($"lookYaw={F(yaw)} lookPitch={F(pitch)}");
            }
            parts.Add($"hips={F(hips.position.y * 100 - v.root.position.y * 100)}");
            return string.Join(" ", parts);
        }

        // ------------------------------------------------------------ the acts' weapons

        /// <summary>unitViews.ts THROWS (syntyFigures.ts): a blade's stroke, its clip and its clock (strike's throwClipTime).</summary>
        sealed class Stroke
        {
            public string clip;
            /// <summary>How much further in it steps for the blow (tiles: the knife's short reach; the review of 9/11, Gameplay).</summary>
            public float closer;
            /// <summary>The clip's time of the blow, how long after the stroke begins it lands (game seconds), the clip's speed till then, when it is over; and from the blow, the clip from `after` at `afterRate`.</summary>
            public float release, lets, rate, end, after, afterRate;
            public float ClipTime(float t) => t >= lets ? after + (t - lets) * afterRate : release + (t - lets) * rate;
        }

        static readonly Stroke KnifeStroke = new Stroke { clip = "knifeStab", release = 0.42f, lets = 0.34f, rate = 1.3f, end = 0.75f, after = 1.0f, afterRate = 0.75f, closer = 0.2f };
        static readonly Stroke MacheteStroke = new Stroke { clip = "macheteSwing", release = 1.0f, lets = 0.34f, rate = 2.0f, end = 0.75f, after = 1.0f, afterRate = 1.7f };
        /// <summary>
        /// From above (Gameplay 29/10, Ana's #10: the pan and the crowbar), Mixamo's axe brought down: its right hand up and
        /// back at 0.45 s, before the chest at 0.83 s (sampled every 0.05 s on a figure); from 0.2 s, the hand rising.
        /// </summary>
        static readonly Stroke ChopStroke = new Stroke { clip = "macheteChop", release = 0.83f, lets = 0.34f, rate = 1.85f, end = 0.75f, after = 0.85f, afterRate = 1.2f };

        /// <summary>The stroke of the blade in its hands: from above if its art says so (WeaponArt.stroke), a knife's stab, anything else the machete's swing.</summary>
        static Stroke StrokeOf(View v)
        {
            var art = v.arts.TryGetValue(v.drawn, out var a) ? a : null;
            if (art?.stroke == "chop") return ChopStroke;
            string blade = art?.fbx ?? "";
            return blade.IndexOf("Knife", StringComparison.OrdinalIgnoreCase) >= 0 ? KnifeStroke : MacheteStroke;
        }

        /// <summary>unitViews.ts lunge: how far it steps in (tiles: the gap less LUNGE_SHORT, LUNGE at most), wound up and striking (seconds), back.</summary>
        const float LungeShort = 0.72f, LungeMost = 0.45f, WindUp = 0.2f, StrokeTime = 0.14f, Back = 0.35f;
        /// <summary>Seconds a blade's stroke takes to let go of the body after its follow-through (Swing, Pry; the 3D review's A-08: 0.15 s, a frame and a half at 10 fps).</summary>
        const float StrokeOut = 0.35f;
        /// <summary>How far into «Zombie Headbutt» the dead's lunge starts it: its blow, 0.45 s in, on lunge's (WindUp + StrokeTime).</summary>
        const float HeadbuttLead = 0.45f - WindUp - StrokeTime;
        /// <summary>
        /// unitViews.ts BITE_GAP: a bite that kills ends this far from its victim's middle (tiles); it lets go this long after if
        /// nothing eats (seconds). 0.42 till the 3D review's A-13 (10/11): holding on, the dead one was inside the soldier, its
        /// head 44 cm out of her back (CombatLab.Mordisco, along the bite from where it stood, tiles: its chest 1.09 and head
        /// 1.23 against her chest 0.99 and head 0.94; 1.03 and 1.14 with M-2's lean off). Now its head at her neck, the chests
        /// a trunk apart: 0.42 + max(1.14 − 0.84, 1.03 − 0.74), to 0.05.
        /// </summary>
        const float BiteGap = 0.7f, BiteHold = 2;

        /// <summary>The way from `home` towards `at` on the ground: the gap less `short` tiles, `most` tiles at most (none if nearer).</summary>
        static Vector3 Reach(Vector3 home, Vector3 at, float @short, float most)
        {
            var gap = at - home;
            gap.y = 0;
            float tiles = Mathf.Clamp(gap.magnitude / MapSpace.Tile - @short, 0, most);
            return gap.sqrMagnitude > 1e-6f ? gap.normalized * tiles * MapSpace.Tile : Vector3.zero;
        }

        /// <summary>From `from` to `to` over `seconds`, eased in and out (a held still when they are the same).</summary>
        static async Awaitable Slide(View v, Vector3 from, Vector3 to, float seconds, CancellationToken ct)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                v.root.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
                await Awaitable.NextFrameAsync(ct);
            }
            v.root.position = to;
        }

        /// <summary>
        /// A bite that kills (unitViews.ts lunge with its feed clip): in to `home` + `reach` over the clip's `seconds`, there
        /// by 0.62 of them; then, if nothing eats it within BiteHold (Feed) and no other bite was taken, it lets go: the
        /// clip off and back home over 0.35 s.
        /// </summary>
        async Awaitable Approach(View v, Vector3 home, Vector3 reach, float seconds, int bite, CancellationToken ct)
        {
            try
            {
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    if (v.feeding || v.bites != bite || v.root == null) return;
                    v.root.position = home + reach * Mathf.SmoothStep(0, 1, Mathf.Min(1, t / seconds * 1.6f));
                    await Awaitable.NextFrameAsync(ct);
                }
                v.root.position = home + reach;
                await Awaitable.WaitForSecondsAsync(BiteHold, ct);
                if (v.feeding || v.bites != bite || v.root == null || v.dead) return;
                v.anim?.StopWhole(0.25f);
                await Slide(v, v.root.position, home, 0.35f, ct);
                v.home = Vector3.zero;
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// A living one's blow at `target` (unitViews.ts lunge, syntyFigures.ts strike): the blade drawn from the belt (or its
        /// own, held), wound up 0.2 s, the stroke 0.14 s stepping in, `onHit` as it lands (0.34 s), the stroke's clip on its
        /// clock over the upper body (THROWS: knife, machete; a baton the machete's), back again in 0.35 s, the main weapon
        /// drawn once more. Resolved as the blow lands (unitViews.ts lunge): the stroke and the step back go on by
        /// themselves (StrokeOn).
        /// </summary>
        async Awaitable Strike(View v, View target, Action onHit, CancellationToken ct)
        {
            bool landed = false;
            _ = StrokeOn(v, target, () =>
            {
                if (landed) return;
                landed = true;
                onHit?.Invoke();
            }, ct);
            while (!landed && !ct.IsCancellationRequested) await Awaitable.NextFrameAsync(ct);
        }

        /// <summary>
        /// The stroke, on by itself (Strike). One that fails (a bone missing, a clip not loaded, its figure gone) lands all
        /// the same, the error in red.log: Strike waits for `land`, and the blow's damage shows with it (the review of
        /// 10/11, row 3: else the combat waited for it for ever).
        /// </summary>
        async Awaitable StrokeOn(View v, View target, Action land, CancellationToken ct)
        {
            v.striking = true;
            try
            {
                await Swing(v, target, land, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                v.striking = false;
                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        land();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        async Awaitable Swing(View v, View target, Action onHit, CancellationToken ct)
        {
            // A blow at arm's length (unitViews.ts rtStrike: struck).
            StruckBy(target, v.root.position, true);
            // (From the guard if it was on guard with the blade: the stroke had begun from the blade hanging, the guard
            // undone in 0.1 s; the 3D review's A-08, 10/11.)
            bool guarded = Guarding(v);
            Draw(v, BeltCarry);
            if (guarded) Guard(v, 0.1f);
            else AtRest(v, 0.1f);
            var stroke = StrokeOf(v);
            var clip = FigureCatalog.Load()?.Clip(stroke.clip);
            var home = v.root.position;
            var toward = Vector3.zero;
            if (target != null)
            {
                var gap = target.root.position - home;
                gap.y = 0;
                // (The knife 0.2 tiles further in: its middle stopped 42 cm short of the chest, CombatLab.Cuerpo.)
                float tiles = Mathf.Clamp(gap.magnitude / MapSpace.Tile - LungeShort + stroke.closer, 0, LungeMost + stroke.closer);
                toward = gap.normalized * tiles * MapSpace.Tile;
            }
            bool hit = false;
            float total = WindUp + StrokeTime + Back;
            // (Out of the stroke over StrokeOut, from where it began going out before, not in 0.15 s from the most bent of
            // its follow-through: the 3D review's A-08, 10/11.)
            float over = stroke.end + StrokeOut - 0.15f;
            for (float t = 0; t < Mathf.Max(total, over); t += Time.deltaTime)
            {
                if (clip != null && v.anim != null)
                {
                    float share = Mathf.Clamp01(Mathf.Min(t / 0.06f, (over - t) / StrokeOut));
                    v.anim.Scrub(t < over ? clip : null, Mathf.Clamp(stroke.ClipTime(t), 0, clip.length), share, true);
                }
                if (t < WindUp) v.root.position = home;
                else if (t < WindUp + StrokeTime) v.root.position = home + toward * ((t - WindUp) / StrokeTime);
                else if (t < total) v.root.position = home + toward * (1 - (t - WindUp - StrokeTime) / Back);
                else v.root.position = home;
                if (!hit && t >= stroke.lets)
                {
                    hit = true;
                    onHit?.Invoke();
                }
                await Awaitable.NextFrameAsync(ct);
            }
            if (!hit) onHit?.Invoke();
            v.anim?.Scrub(null, 0, 0, true);
            if (v.root == null) return;
            v.root.position = home;
            // (On watch with the blade, back on guard.)
            Draw(v, v.watch && v.guard ? BeltCarry : MainCarry);
            Settle(v, 0.2f);
        }

        /// <summary>
        /// The crowbar at a boarded window (Arturo's A11 of 2/11, BoardsRemovedEvent): turned to `place`, the bar drawn from
        /// the belt and brought down on the boards as a blow from above (ChopStroke), with no step in; as it lands the heap
        /// topples (MapView.Topple), then the bar is put away. Without the bar in hand (no figure, no crowbar), a search's
        /// rummage. UI's event player waits for it.
        /// </summary>
        public async Awaitable Pry(string id, Vector3 place, Pos tile, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || v.anim == null || !v.carried.ContainsKey(BeltCarry) || v.arts[BeltCarry].fbx != Anims.Weapon("crowbar")?.fbx)
            {
                await Rummage(id, place, ct);
                Map.MapView.Current?.Topple(tile);
                await Awaitable.WaitForSecondsAsync(0.45f, ct);
                return;
            }
            Face(id, place);
            v.striking = true;
            var home = v.root.position;
            try
            {
                Draw(v, BeltCarry);
                AtRest(v, 0.1f);
                var clip = FigureCatalog.Load()?.Clip(ChopStroke.clip);
                bool toppled = false;
                // (Out over StrokeOut, as a blow: the 3D review's A-08, 10/11.)
                float over = ChopStroke.end + StrokeOut - 0.15f;
                // A step in to the boards as a blow's (the 3D review's A-20, 10/11: from the tile beside them, 1.5 m off, the bar
                // came down half a metre short of them), in as it winds up, kept till they fall, back over Back.
                var toward = Reach(home, place, PryShort, PryMost);
                float stay = PryLets + 0.1f;
                for (float t = 0; t < Mathf.Max(ChopStroke.end + 0.45f, over, stay + Back); t += Time.deltaTime)
                {
                    float share = 0;
                    if (clip != null)
                    {
                        share = Mathf.Clamp01(Mathf.Min(t / 0.06f, (over - t) / StrokeOut));
                        v.anim.Scrub(t < over ? clip : null, Mathf.Clamp(ChopStroke.ClipTime(t), 0, clip.length), share, true);
                    }
                    // (Its left hand on the bar as the stroke has the body: «Downward Attack With Axe» is one-handed, its
                    // left 0.8 to 1.45 m from the right, ClipProbe.Alone BASTION_MANOS=1; on the stroke's layer the left arm
                    // stayed in the hip of its rest.)
                    v.pryHand = share;
                    v.root.position = t < WindUp ? home : t < WindUp + StrokeTime ? home + toward * ((t - WindUp) / StrokeTime)
                        : t < stay ? home + toward : t < stay + Back ? home + toward * (1 - (t - stay) / Back) : home;
                    // (The boards fall as the bar is at its lowest before them, PryLets, not as the stroke begins to come down.)
                    if (!toppled && t >= PryLets)
                    {
                        toppled = true;
                        Map.MapView.Current?.Topple(tile);
                    }
                    await Awaitable.NextFrameAsync(ct);
                }
                v.anim.Scrub(null, 0, 0, true);
            }
            finally
            {
                v.striking = false;
                v.pryHand = 0;
                if (v.root != null) v.root.position = home;
                if (v.root != null)
                {
                    Draw(v, MainCarry);
                    Settle(v, 0.2f);
                }
            }
        }

        /// <summary>
        /// The crowbar at the boards (Pry; the 3D review's A-20, 10/11): how far short of the boards' place it steps in to
        /// (tiles, as a blow's LungeShort; PryMost at most), and when they fall (seconds into the stroke: the bar at its
        /// lowest before them, «Downward Attack With Axe» at 1.2 s, CombatLab.Palanca's [palanca]; ChopStroke.lets, 0.34, had
        /// them fall with the bar still over the head). How far down the bar from the right hand the left takes it (m).
        /// </summary>
        const float PryShort = 0.83f, PryMost = 0.45f, PryLets = 0.68f, PryLeft = 0.25f;

        /// <summary>The left hand onto the crowbar PryLeft down it from the right as far as View.pryHand (A-20), by two-bone IK as Support's.</summary>
        static void PryHand(View v)
        {
            if (v.pryHand <= 0 || v.animator == null || !(BladeLine(v) is var (grip, tip))) return;
            var a = v.animator;
            Transform shoulder = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), elbow = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(HumanBodyBones.LeftHand),
                knuckle = a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            if (shoulder == null || elbow == null || hand == null || knuckle == null || (tip - grip).sqrMagnitude < 1e-6f) return;
            Vector3 Palm() => Vector3.Lerp(hand.position, knuckle.position, 0.6f);
            var on = grip + (tip - grip).normalized * PryLeft * (v.body != null ? v.body.lossyScale.x / Figures.CharacterScale : 1);
            var goal = Vector3.Lerp(Palm(), on, v.pryHand);
            for (int pass = 0; pass < 2; pass++)
                Reach(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, hand.position + (goal - Palm()), elbow.position - shoulder.position, 0.005f);
        }

        /// <summary>
        /// A reload this long, its clip's reload scrubbed through on an ease (spin), coming in and going over its ends
        /// (ReloadEnds of it). 0.6 s in 0.10.1 (unitViews.ts reload), the whole of «Reloading» (3.3 s) in it: the rifle
        /// flicked up over the shoulder. Arturo, 9/11 (the review, in the box): 1.2 to 1.5 s, the magazine out and in seen.
        /// </summary>
        const float ReloadSeconds = 1.3f, ReloadEnds = 0.12f;
        /// <summary>
        /// How steep a long gun goes at most as it reloads (degrees over the level; ReloadTilt; BASTION_RECARGA_TOPE for the
        /// labs). The card asked 45, the 9/11 diagonal by the face, but measured (CombatLab.Agarres, up=) the reload went to
        /// 34–35° standing and 45° knelt, not the 70–90° it was thought: 45 changed nothing. Tried 30 and 20: 30.
        /// </summary>
        static readonly float ReloadTiltMost = LabFloat("BASTION_RECARGA_TOPE", 30);
        /// <summary>
        /// The rifle's reload (the review of 9/11), standing and knelt: Mixamo's «Reload 3» («Walk While Reloading Rifle»)
        /// from the left hand going down for the magazine to it back on the handguard. «Reloading», 0.10.1's, holds the
        /// rifle 45–50° up all through its reload, upright over the shoulder at 1.3 s; «Reload 2» («Reload Rifle While In
        /// Crouch Position») bows the trunk down to the knees, standing or knelt (CombatLab.Agarres, compared at 1.3 s).
        /// </summary>
        static (string clip, float from, float to) RifleReload(View v) => ("rifleReloadWalk", 1.0f, 3.0f);

        /// <summary>
        /// A rifle's reload (unitViews.ts reload, syntyFigures.ts direct): «Reloading» over the upper body scrubbed through
        /// in 0.6 s on an ease, coming in and going over its ends (0.12 of it), the gun held fast in the right hand
        /// meanwhile. The pistol's, «Reloading 2»'s reload the same way (PistolReloadFrom; 0.10.1 had none).
        /// </summary>
        static string ArmsKey(Unit u) => u.Arms != null ? $"{u.Arms.Primary}|{u.Arms.Melee}" : "";

        /// <summary>
        /// A weapon taken in hand from the backpack (unitViews.ts equip): the one it held turned over and down as a reload's
        /// (the reload's stretch of its clip, RifleReload, over the eased 0.7 s), and halfway the new ones in its hands and
        /// where they ride (Arm).
        /// </summary>
        async Awaitable Equip(View v, Unit u)
        {
            const float seconds = 0.7f;
            var (id, from, to) = RifleReload(v);
            var clip = Rifled(v) ? FigureCatalog.Load()?.Clip(id) : null;
            bool swapped = false;
            for (float t = 0; t < seconds && v.root != null; t += Time.deltaTime)
            {
                float k = t / seconds, spin = k < 0.5f ? 2 * k * k : 1 - (-2 * k + 2) * (-2 * k + 2) / 2;
                if (clip != null && v.anim != null)
                {
                    v.reload = spin > 0 && spin < 1 ? Mathf.Min(1, spin / ReloadEnds, (1 - spin) / ReloadEnds) : 0;
                    // (None of it, none at all: a clip plugged in at nothing showed whole for a frame.)
                    v.anim.Scrub(v.reload > 0 ? clip : null, from + spin * (Mathf.Min(to, clip.length) - from), v.reload);
                }
                if (k >= 0.5f && !swapped)
                {
                    swapped = true;
                    Rearm(v, u);
                }
                await Awaitable.NextFrameAsync();
            }
            if (!swapped && v.root != null) Rearm(v, u);
            v.reload = 0;
            v.anim?.Scrub(null, 0, 0);
        }

        /// <summary>What it carried gone, and its weapons as `u` has them now, at rest (Arm, Settle).</summary>
        void Rearm(View v, Unit u)
        {
            foreach (var t in v.carried.Values) if (t != null) DestroyNow(t.gameObject);
            foreach (var t in v.stows.Values) if (t != null) DestroyNow(t.gameObject);
            v.carried.Clear();
            v.arts.Clear();
            v.muzzles.Clear();
            v.middles.Clear();
            v.stows.Clear();
            v.weapon = null;
            v.sheath = null;
            if (Arm(v, u, FigureCatalog.Load())) Settle(v, 0.15f);
            // (Its way of waiting with what it has now: a pistol found, or its gun gone and the blade in the hand.)
            string stance = StanceOf(v, u, v.carried.Count > 0);
            if (stance != v.stance && v.anim != null)
            {
                v.stance = stance;
                if (!v.slumped) v.anim.SetMotion("idle", FigureCatalog.Load()?.Clip(Anims.Stance(v.build, stance) ?? Anims.Motion(v.build, "idle", v.deadKind)));
            }
        }

        /// <summary>
        /// The pistol's reload (the review of 9/11, Gameplay: «Reload» or «Reloading 2», whichever fits): «Reloading 2»
        /// («Gun Reload While Walking») over the upper body, its reload alone, from PistolReloadFrom to PistolReloadTo (the
        /// gun up by the right shoulder from 0.6 s, the magazine in at 1.4–1.8, the slide at 2.0–2.4, back to the aim by
        /// 3.0: ClipProbe.Alone BASTION_MANOS=1), on the rifle's clock (ReloadSeconds).
        /// </summary>
        const float PistolReloadFrom = 0.6f, PistolReloadTo = 3.0f;

        async Awaitable ReloadNow(View v, CancellationToken ct)
        {
            bool pistol = v.inHand == Held.Pistol;
            var (id, from, to) = pistol ? ("pistolReload", PistolReloadFrom, PistolReloadTo) : RifleReload(v);
            var clip = FigureCatalog.Load()?.Clip(id);
            if (v.anim == null || clip == null || (v.inHand != Held.Rifle && !pistol))
            {
                await Awaitable.WaitForSecondsAsync(ReloadSeconds, ct);
                return;
            }
            to = Mathf.Min(to, clip.length);
            // The magazine out in the left hand and back in (Arturo, 9/11: «se ve sacar y meter el cargador»): the gun's own
            // (Apocalypse's guns carry it apart), taken by the hand as the clip's hand leaves the gun and put back as it
            // comes to it again, eased home over MagHome of the clip.
            var gun = v.weapon;
            // (Military's pistol's is «SM_Wep_Pistol_Mag_01»: it never came out. The 3D review's P-1, 10/11.)
            var mag = MagazineOf(gun);
            var hand = v.animator != null ? v.animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            var knuckle = v.animator != null ? v.animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) : null;
            // (Its middle in its own frame: what goes into the palm.)
            var middle = mag != null && mag.GetComponent<Renderer>() is Renderer mr ? mag.InverseTransformPoint(mr.bounds.center) : Vector3.zero;
            Vector3 grabbedAt = default;
            Vector3 homeAt = mag != null ? mag.localPosition : default;
            Quaternion homeTurn = mag != null ? mag.localRotation : default;
            Vector3 backAt = default;
            Quaternion backTurn = default;
            var (outAt, inAt) = pistol ? PistolMag : RifleMag;
            try
            {
                for (float t = 0; t < ReloadSeconds; t += Time.deltaTime)
                {
                    float k = t / ReloadSeconds;
                    float spin = k < 0.5f ? 2 * k * k : 1 - (-2 * k + 2) * (-2 * k + 2) / 2;
                    v.reload = spin > 0 && spin < 1 ? Mathf.Min(1, spin / ReloadEnds, (1 - spin) / ReloadEnds) : 0;
                    float at = from + spin * (to - from);
                    v.reloadAt = at;
                    v.anim.Scrub(clip, at, v.reload);
                    if (mag != null && hand != null && v.weapon == gun)
                    {
                        // (The pistol's: the old one dropped out of sight as the left hand leaves it, PistolEject, and the new one
                        // in that hand from its pouch at the hip, at outAt: P-1. A rifle's comes out in the hand.)
                        if (pistol && at >= PistolEject && at < outAt && mag.parent == gun && mag.gameObject.activeSelf) mag.gameObject.SetActive(false);
                        if (at >= outAt && at < inAt && mag.parent != hand)
                        {
                            mag.SetParent(hand, true);
                            grabbedAt = mag.localPosition;
                            if (pistol && knuckle != null)
                            {
                                var palm = Vector3.Lerp(hand.position, knuckle.position, 0.6f);
                                grabbedAt = hand.InverseTransformPoint(palm - (mag.TransformPoint(middle) - mag.position));
                                mag.gameObject.SetActive(true);
                            }
                        }
                        else if (at >= inAt && mag.parent == hand)
                        {
                            mag.SetParent(gun, true);
                            (backAt, backTurn) = (mag.localPosition, mag.localRotation);
                        }
                        // (Out: into the palm over MagGrab of the clip, its middle there, as it was turned.)
                        if (mag.parent == hand && knuckle != null)
                        {
                            var palm = Vector3.Lerp(hand.position, knuckle.position, 0.6f);
                            var inPalm = hand.InverseTransformPoint(palm - (mag.TransformPoint(middle) - mag.position));
                            mag.localPosition = Vector3.Lerp(grabbedAt, inPalm, Mathf.SmoothStep(0, 1, Mathf.Clamp01((at - outAt) / MagGrab)));
                        }
                        if (at >= inAt && mag.parent == gun)
                        {
                            float home = Mathf.SmoothStep(0, 1, Mathf.Clamp01((at - inAt) / MagHome));
                            mag.localPosition = Vector3.Lerp(backAt, homeAt, home);
                            mag.localRotation = Quaternion.Slerp(backTurn, homeTurn, home);
                        }
                    }
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            finally
            {
                if (mag != null && gun != null)
                {
                    mag.gameObject.SetActive(true);
                    mag.SetParent(gun, false);
                    mag.localPosition = homeAt;
                    mag.localRotation = homeTurn;
                }
            }
            v.reload = 0;
            v.reloadAt = 0;
            v.anim.Scrub(null, 0, 0);
        }

        /// <summary>
        /// When the magazine leaves the gun in the left hand and comes back (seconds into the reload's clip: the left hand
        /// at the magazine, down at the hip, and up to the gun again; ClipProbe.Alone BASTION_MANOS=1), and over how much
        /// of the clip it eases home. The pistol's (the 3D review's P-1, 10/11): «Reloading 2» on a Sidekick body never brings
        /// the hands within 28 cm; the left leaves the gun's side by 0.7 s, is down at the hip from 0.8 to 1.85, nearest the
        /// pistol at 2.27 (the magazine in) and 2.79 (the slide), 28 cm off: PistolSupport takes it the rest of the way. So the
        /// old magazine drops out of sight as the hand goes to the hip (PistolEject), the new one is in it from its pouch there
        /// (1.3), and it goes in at 2.27.
        /// </summary>
        static readonly (float, float) RifleMag = (1.05f, 2.2f), PistolMag = (1.3f, 2.27f);
        /// <summary>When the pistol's old magazine goes (seconds into «Reloading 2»: the left hand away from it to the hip; P-1).</summary>
        const float PistolEject = 0.75f;

        /// <summary>
        /// The pistol's reload (P-1): the left palm onto it by two-bone IK about the clip's moments (seconds of «Reloading
        /// 2», each a bell PistolBell wide, whole within PistolBellTop of it): the magazine in at the grip's foot, the slide
        /// as it is racked; as far as the reload is in, and no more than SupportMost from where the clip has the hand.
        /// </summary>
        static readonly float[] PistolGrabs = { 2.27f, 2.79f };
        const float PistolBell = 0.25f, PistolBellTop = 0.08f;
        /// <summary>How far under the pistol's grip (its origin, in the right palm) the magazine goes in (metres).</summary>
        const float MagWell = 0.06f;

        static void PistolSupport(View v)
        {
            if (v.inHand != Held.Pistol || v.reload <= 0 || v.weapon == null || v.animator == null || !v.animator.isHuman || v.dead) return;
            float share = 0;
            int which = -1;
            for (int k = 0; k < PistolGrabs.Length; k++)
            {
                float off = Mathf.Abs(v.reloadAt - PistolGrabs[k]);
                float w = off <= PistolBellTop ? 1 : 1 - Mathf.SmoothStep(0, 1, (off - PistolBellTop) / (PistolBell - PistolBellTop));
                if (w > share)
                {
                    share = w;
                    which = k;
                }
            }
            share *= v.reload;
            if (share <= 0.001f) return;
            var gun = v.weapon;
            // (The magazine where it rides in the gun, out or in; the slide, racked.)
            Vector3 goal;
            var slide = which == 1 ? gun.GetComponentsInChildren<Transform>().FirstOrDefault(t => t != gun && t.name.Contains("Slide")) : null;
            var mag = MagazineOf(gun);
            if (slide != null && slide.GetComponent<Renderer>() is Renderer sr) goal = sr.bounds.center;
            else if (mag != null && mag.GetComponent<Renderer>() is Renderer mr && mag.parent == gun) goal = mr.bounds.center;
            // (Out of it, in the hand: to the grip's foot, where it goes in.)
            else goal = gun.position + gun.rotation * Vector3.down * MagWell;
            var a = v.animator;
            Transform shoulder = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), elbow = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(HumanBodyBones.LeftHand),
                knuckle = a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            if (shoulder == null || elbow == null || hand == null || knuckle == null) return;
            Vector3 Palm() => Vector3.Lerp(hand.position, knuckle.position, 0.6f);
            var want = goal - Palm();
            if (want.magnitude > SupportMost) want = want.normalized * SupportMost;
            var to = Palm() + want * share;
            for (int pass = 0; pass < 2; pass++)
                Reach(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, hand.position + (to - Palm()), elbow.position - shoulder.position, 0.005f);
        }
        const float MagHome = 0.2f, MagGrab = 0.15f;
    }
}
