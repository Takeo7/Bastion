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
        /// blade (else the knife). Anyone else's from its template: UNIT_WEAPONS at its tier, the pistol as a second gun, a
        /// squad soldier's machete for a sword.
        /// </summary>
        static (string weapon, string sidearm, string belt) ArmamentOf(Unit u)
        {
            int tier = Mathf.Clamp(u.WeaponTier ?? 1, 1, 3);
            if (u.Arms != null && SquadTemplates.Contains(u.Template))
            {
                string primary = u.Arms.Primary;
                return (primary != null ? Anims.FoundModel(primary, tier) : "pistol", primary != null ? "pistol" : null, u.Arms.Melee != null ? Anims.FoundModel(u.Arms.Melee, tier) : "knife");
            }
            Content.Templates.TryGetValue(u.Template ?? "", out var t);
            return (Anims.UnitWeapon(u.Template, tier),
                t != null && t.Secondary == "pistol" && t.Weapon != "pistol" ? "pistol" : null,
                t != null && SquadTemplates.Contains(u.Template) && t.MeleeWeapon == "sword" ? "machete" : null);
        }

        /// <summary>The pistol's model under `parent`, at the figures' scale (the Settlement's soldiers whose only gun it is).</summary>
        public static Transform Pistol(Transform parent, FigureCatalog catalog) => WeaponModel(Anims.Weapon("pistol"), parent, catalog, out _);

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
            foreach (var part in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Silencer")).ToList()) part.gameObject.SetActive(false);
            var box = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                foreach (var corner in Corners(r.bounds)) box.Encapsulate(model.transform.InverseTransformPoint(corner));
            muzzle = art.muzzle != null && art.muzzle.Length >= 3 ? new Vector3(art.muzzle[0], art.muzzle[1], art.muzzle[2]) : new Vector3(0, 0.02f, box.max.z);
            model.transform.localScale = Vector3.one * Figures.CharacterScale * (art.size > 0 ? art.size : 1);
            foreach (var c in model.GetComponentsInChildren<Collider>()) DestroyNow(c);
            return model.transform;
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
            }
            if (v.carried.Count == 0) return false;
            Stows(v);
            v.rest = RestFor(v.seed);
            Draw(v, MainCarry);
            return true;
        }

        /// <summary>Whether its main weapon is a two-handed gun (it moves and waits in the rifle's clips).</summary>
        static bool Rifled(View v) => v.arts.TryGetValue(MainCarry, out var a) && !a.pistol && !a.melee;

        /// <summary>Whether its main weapon is the pistol (its own way of waiting, its arms its own as it moves).</summary>
        static bool PistolMain(View v) => v.arts.TryGetValue(MainCarry, out var a) && a.pistol;

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
            Hang(MainCarry, chest, new Vector3(0, s3.y + 0.02f, s3.z - 0.11f), new Vector3(0.45f, 0.9f, 0), new Vector3(0, 0, -1));
            var hipR = In(HumanBodyBones.RightUpperLeg);
            Hang(SidearmCarry, HumanBodyBones.RightUpperLeg, new Vector3(hipR.x + 0.075f, hipR.y - HolsterDrop, hipR.z + 0.01f), new Vector3(0, -1, 0.1f), new Vector3(0, 0, 1));
            var hips = In(HumanBodyBones.Hips);
            var hipL = In(HumanBodyBones.LeftUpperLeg);
            Hang(BeltCarry, HumanBodyBones.Hips, new Vector3(hips.x, hipL.y + 0.03f, hips.z) + BeltOut * BeltOutBy, BeltAlong, Vector3.Cross(BeltOut, BeltAlong));
            // (A blade too big for the belt, the chainsaw: across the back the other way, over the left shoulder, behind the gun.)
            Hang(SlungCarry, chest, new Vector3(0, s3.y - 0.02f, s3.z - 0.2f), new Vector3(-0.45f, 0.9f, 0), new Vector3(0, 0, -1));
        }

        /// <summary>
        /// syntyFigures.ts draw and hold: `which` in the hands (one it doesn't carry: the main one), the others where they
        /// ride (the main gun on the back, or in the holster when it is the only gun, a pistol); at once, as 0.10.1 did.
        /// </summary>
        static void Draw(View v, string which)
        {
            if (!v.carried.ContainsKey(which)) which = MainCarry;
            if (!v.carried.ContainsKey(which)) which = v.carried.Keys.FirstOrDefault();
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
                string ride = kv.Key == MainCarry && pistolOnly ? SidearmCarry : kv.Key == BeltCarry && v.arts[kv.Key].slung ? SlungCarry : kv.Key;
                if (!v.stows.TryGetValue(ride, out var mount))
                {
                    model.gameObject.SetActive(false);
                    continue;
                }
                model.gameObject.SetActive(true);
                model.SetParent(mount, false);
                model.localRotation = Quaternion.identity;
                float lossy = Mathf.Max(mount.lossyScale.x, 1e-4f);
                model.localScale = Vector3.one * Figures.CharacterScale * Size(v.arts[kv.Key]) / lossy;
                // (The gun by its middle; the chainsaw nearer its motor, so that the motor sits in the middle of the back, not
                // at the waist: Gameplay 29/10.)
                model.localPosition = new Vector3(0, 0, ride == MainCarry ? -v.middles[kv.Key] / lossy : ride == SlungCarry ? -v.middles[kv.Key] * SlungBy / lossy : 0);
                // A blade at the belt by its grip, along the way it runs (the pan by its handle, hanging).
                if (ride == BeltCarry) Seat(model, v.arts[kv.Key], mount.position, mount.rotation);
            }
            v.weapon = v.carried.TryGetValue(which, out var held) ? held : null;
            if (v.weapon != null) v.weapon.gameObject.SetActive(true);
            v.muzzle = v.muzzles.TryGetValue(which, out var m) ? m : Vector3.zero;
            v.inHand = v.arts.TryGetValue(which, out var art) ? art.pistol ? Held.Pistol : art.melee ? Held.Blade : Held.Rifle : Held.None;
            // (The rifle's turn in the hand kept: drawn again, it is held as it was, not level at the waist for a frame.)
            v.pistolGrip = null;
            // In the hand at once (else, for the frame, where the root is: at the feet).
            HoldNow(v);
        }

        static float Size(WeaponArt art) => art != null && art.size > 0 ? art.size : 1;

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

        /// <summary>Whether its weapon is up: aimed for a shot, at a target being picked, or on watch.</summary>
        static bool Up(View v) => v.aiming || v.picking != null || v.watch;

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
            v.anim.Rest(catalog.Clip(KnifeStroke.clip), fade, GuardAt);
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
            if (v.close > 0 && v.inHand == Held.Rifle && v.reachOf == v.drawn && v.reach > 0)
                want = Mathf.Min(CloseBack, Mathf.Max(0, v.reach - (v.close - CloseGap))) * v.up;
            if (want == v.back) return;
            // (Eased as the gun comes up; the reach is only measured once it is all the way up.)
            v.back = Mathf.MoveTowards(v.back, want, dt * CloseBack / AimBlend);
            v.body.localPosition = new Vector3(0, 0, -v.back * MapSpace.Tile);
        }

        /// <summary>syntyFigures.ts aimReach: how far ahead of its feet the muzzle of the long gun in its hands comes, measured while it is all the way up.</summary>
        static void Reach(View v)
        {
            if (v.inHand != Held.Rifle || v.weapon == null || v.up < 0.99f || v.reload > 0 || v.dead) return;
            var muzzle = v.weapon.TransformPoint(v.muzzle) - v.root.position;
            v.reach = Vector3.Dot(muzzle, v.root.forward) / MapSpace.Tile + v.back;
            v.reachOf = v.drawn;
        }

        /// <summary>Aimed with what it holds: a rifle's aim (knelt, the kneeling one), the pistol in both hands (pistolAim, syntyFigures.ts direct).</summary>
        static void Aimed(View v, float fade)
        {
            var catalog = FigureCatalog.Load();
            if (v.anim == null || catalog == null) return;
            v.anim.ArmRest(null, 0, fade);
            v.anim.Rest(catalog.Clip(v.inHand == Held.Pistol ? "pistolAim" : v.crouched ? "rifleKneelAim" : "rifleAim"), fade);
        }

        /// <summary>What it holds where the hands hold it, after the body's pose (syntyFigures.ts clipHold): a gun between them, the pistol along the forearm, a blade in the fist.</summary>
        static void HoldNow(View v)
        {
            if (v.weapon == null || v.animator == null) return;
            switch (v.inHand)
            {
                case Held.Pistol:
                    HoldPistol(v);
                    break;
                case Held.Blade:
                    HoldBlade(v);
                    break;
                default:
                    Hold(v.animator, v.weapon, v.root, v.dead || v.reload > 0, ref v.grip);
                    break;
            }
        }

        /// <summary>
        /// The pistol in the right palm, its barrel along the forearm (syntyFigures.ts clipGrip for a pistol: hand − elbow),
        /// that turn learnt in the hand's frame little by little, levelled as it is aimed (forward.y by 1 − aim).
        /// </summary>
        static void HoldPistol(View v) => HoldPistol(v.animator, v.weapon, v.root, v.aim, ref v.pistolGrip);

        /// <summary>HoldPistol(View) for any figure: `a`'s right hand on `pistol`, `root` its upright, aimed `aim` (0–1), `grip` its turn learnt in the hand.</summary>
        public static void HoldPistol(Animator a, Transform pistol, Transform root, float aim, ref Quaternion? grip)
        {
            if (a == null || pistol == null) return;
            Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand), elbow = a.GetBoneTransform(HumanBodyBones.RightLowerArm), knuckle = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (hand == null || elbow == null) return;
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
                var level = new Vector3(ahead.x, ahead.y * (1 - aim), ahead.z);
                if (level.sqrMagnitude > 1e-6f) turn = Quaternion.LookRotation(level, Vector3.ProjectOnPlane(Vector3.up, level));
            }
            pistol.SetPositionAndRotation(palm, turn);
        }

        /// <summary>
        /// A blade in the right fist as the hand holds it (syntyFigures.ts clipHold with no clipGrip, 0.10.1's weaponLab
        /// knife and machete): its blade on along the fingers (down by the leg with the arm hanging, out along the arm as it
        /// swings), its back towards the index's side.
        /// </summary>
        static void HoldBlade(View v)
        {
            var a = v.animator;
            Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand), index = a.GetBoneTransform(HumanBodyBones.RightIndexProximal),
                little = a.GetBoneTransform(HumanBodyBones.RightLittleProximal), middle = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (hand == null || index == null || little == null || middle == null) return;
            var palm = Vector3.Lerp(hand.position, middle.position, 0.6f);
            var fingers = (middle.position - hand.position).normalized;
            var across = Vector3.ProjectOnPlane(index.position - little.position, fingers);
            var art = v.arts.TryGetValue(v.drawn, out var a2) ? a2 : null;
            Seat(v.weapon, art, palm, Quaternion.LookRotation(fingers, across.sqrMagnitude > 1e-6f ? across : v.root.up));
            // Two-handed (the chainsaw): the left hand on its other grip, the elbow out to the left and down.
            if (art?.two != null && art.two.Length >= 3)
                Reach(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, v.weapon.TransformPoint(new Vector3(art.two[0], art.two[1], art.two[2])),
                    v.root.TransformDirection(new Vector3(-1, -0.7f, 0)), 0.02f);
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
            return $"{v.drawn} drawn ({v.inHand}); " + string.Join("; ", v.carried.Select(kv =>
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

        // ------------------------------------------------------------ the acts' weapons

        /// <summary>unitViews.ts THROWS (syntyFigures.ts): a blade's stroke, its clip and its clock (strike's throwClipTime).</summary>
        sealed class Stroke
        {
            public string clip;
            /// <summary>The clip's time of the blow, how long after the stroke begins it lands (game seconds), the clip's speed till then, when it is over; and from the blow, the clip from `after` at `afterRate`.</summary>
            public float release, lets, rate, end, after, afterRate;
            public float ClipTime(float t) => t >= lets ? after + (t - lets) * afterRate : release + (t - lets) * rate;
        }

        static readonly Stroke KnifeStroke = new Stroke { clip = "knifeStab", release = 0.42f, lets = 0.34f, rate = 1.3f, end = 0.75f, after = 1.0f, afterRate = 0.75f };
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
        /// <summary>How far into «Zombie Headbutt» the dead's lunge starts it: its blow, 0.45 s in, on lunge's (WindUp + StrokeTime).</summary>
        const float HeadbuttLead = 0.45f - WindUp - StrokeTime;
        /// <summary>unitViews.ts BITE_GAP: a bite that kills ends this far from its victim's middle (tiles); it lets go this long after if nothing eats (seconds).</summary>
        const float BiteGap = 0.42f, BiteHold = 2;

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
                landed = true;
                onHit?.Invoke();
            }, ct);
            while (!landed && !ct.IsCancellationRequested) await Awaitable.NextFrameAsync(ct);
        }

        async Awaitable StrokeOn(View v, View target, Action onHit, CancellationToken ct)
        {
            v.striking = true;
            try
            {
                await Swing(v, target, onHit, ct);
            }
            finally
            {
                v.striking = false;
            }
        }

        async Awaitable Swing(View v, View target, Action onHit, CancellationToken ct)
        {
            // A blow at arm's length (unitViews.ts rtStrike: struck).
            StruckBy(target, v.root.position, true);
            Draw(v, BeltCarry);
            AtRest(v, 0.1f);
            var stroke = StrokeOf(v);
            var clip = FigureCatalog.Load()?.Clip(stroke.clip);
            var home = v.root.position;
            var toward = Vector3.zero;
            if (target != null)
            {
                var gap = target.root.position - home;
                gap.y = 0;
                float tiles = Mathf.Clamp(gap.magnitude / MapSpace.Tile - LungeShort, 0, LungeMost);
                toward = gap.normalized * tiles * MapSpace.Tile;
            }
            bool hit = false;
            float total = WindUp + StrokeTime + Back;
            for (float t = 0; t < Mathf.Max(total, stroke.end); t += Time.deltaTime)
            {
                if (clip != null && v.anim != null)
                {
                    float share = Mathf.Clamp01(Mathf.Min(t / 0.06f, (stroke.end - t) / 0.15f));
                    v.anim.Scrub(t < stroke.end ? clip : null, Mathf.Clamp(stroke.ClipTime(t), 0, clip.length), share);
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
            v.anim?.Scrub(null, 0, 0);
            if (v.root == null) return;
            v.root.position = home;
            // (On watch with the blade, back on guard.)
            Draw(v, v.watch && v.guard ? BeltCarry : MainCarry);
            Settle(v, 0.2f);
        }

        /// <summary>unitViews.ts reload: this long, the clip scrubbed through on an ease (spin), coming in and going over its ends.</summary>
        const float ReloadSeconds = 0.6f, ReloadEnds = 0.12f;

        /// <summary>
        /// A rifle's reload (unitViews.ts reload, syntyFigures.ts direct): «Reloading» over the upper body scrubbed through
        /// in 0.6 s on an ease, coming in and going over its ends (0.12 of it), the gun held fast in the right hand
        /// meanwhile. The pistol has none (0.10.1: its rest or its aim as they were).
        /// </summary>
        static string ArmsKey(Unit u) => u.Arms != null ? $"{u.Arms.Primary}|{u.Arms.Melee}" : "";

        /// <summary>
        /// A weapon taken in hand from the backpack (unitViews.ts equip): the one it held turned over and down as a reload's
        /// (the reload's clip over the eased 0.7 s), and halfway the new ones in its hands and where they ride (Arm).
        /// </summary>
        async Awaitable Equip(View v, Unit u)
        {
            const float seconds = 0.7f;
            var clip = Rifled(v) ? FigureCatalog.Load()?.Clip("rifleReload") : null;
            bool swapped = false;
            for (float t = 0; t < seconds && v.root != null; t += Time.deltaTime)
            {
                float k = t / seconds, spin = k < 0.5f ? 2 * k * k : 1 - (-2 * k + 2) * (-2 * k + 2) / 2;
                if (clip != null && v.anim != null)
                {
                    v.reload = spin > 0 && spin < 1 ? Mathf.Min(1, spin / ReloadEnds, (1 - spin) / ReloadEnds) : 0;
                    // (None of it, none at all: a clip plugged in at nothing showed whole for a frame.)
                    v.anim.Scrub(v.reload > 0 ? clip : null, spin * clip.length, v.reload);
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
            if (Arm(v, u, FigureCatalog.Load())) Settle(v, 0.15f);
        }

        async Awaitable ReloadNow(View v, CancellationToken ct)
        {
            var clip = FigureCatalog.Load()?.Clip("rifleReload");
            if (v.anim == null || clip == null || v.inHand != Held.Rifle)
            {
                await Awaitable.WaitForSecondsAsync(ReloadSeconds, ct);
                return;
            }
            for (float t = 0; t < ReloadSeconds; t += Time.deltaTime)
            {
                float k = t / ReloadSeconds;
                float spin = k < 0.5f ? 2 * k * k : 1 - (-2 * k + 2) * (-2 * k + 2) / 2;
                v.reload = spin > 0 && spin < 1 ? Mathf.Min(1, spin / ReloadEnds, (1 - spin) / ReloadEnds) : 0;
                v.anim.Scrub(clip, spin * clip.length, v.reload);
                await Awaitable.NextFrameAsync(ct);
            }
            v.reload = 0;
            v.anim.Scrub(null, 0, 0);
        }
    }
}
