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
        const string MainCarry = "weapon", SidearmCarry = "sidearm", BeltCarry = "belt";

        /// <summary>The holster this far below the hip (syntyFigures.ts HOLSTER_DROP); the blade out from the hips this way and this far (BELT_OUT, without a body to keep off).</summary>
        const float HolsterDrop = 0.15f, BeltOutBy = 0.13f;
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
                string ride = kv.Key == MainCarry && pistolOnly ? SidearmCarry : kv.Key;
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
                model.localPosition = new Vector3(0, 0, ride == MainCarry ? -v.middles[kv.Key] / lossy : 0);
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
            v.weapon.SetPositionAndRotation(palm, Quaternion.LookRotation(fingers, across.sqrMagnitude > 1e-6f ? across : v.root.up));
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

        /// <summary>unitViews.ts lunge: how far it steps in (tiles: the gap less LUNGE_SHORT, LUNGE at most), wound up and striking (seconds), back.</summary>
        const float LungeShort = 0.72f, LungeMost = 0.45f, WindUp = 0.2f, StrokeTime = 0.14f, Back = 0.35f;

        /// <summary>
        /// A living one's blow at `target` (unitViews.ts lunge, syntyFigures.ts strike): the blade drawn from the belt (or its
        /// own, held), wound up 0.2 s, the stroke 0.14 s stepping in, `onHit` as it lands (0.34 s), the stroke's clip on its
        /// clock over the upper body (THROWS: knife, machete; a baton the machete's), back again in 0.35 s, the main weapon
        /// drawn once more.
        /// </summary>
        async Awaitable Strike(View v, View target, Action onHit, CancellationToken ct)
        {
            // A blow at arm's length (unitViews.ts rtStrike: struck).
            StruckBy(target, v.root.position, true);
            Draw(v, BeltCarry);
            AtRest(v, 0.1f);
            string blade = v.arts.TryGetValue(v.drawn, out var art) ? art.fbx : "";
            var stroke = blade.IndexOf("Knife", StringComparison.OrdinalIgnoreCase) >= 0 ? KnifeStroke : MacheteStroke;
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
            v.root.position = home;
            Draw(v, MainCarry);
            AtRest(v, 0.2f);
        }

        /// <summary>unitViews.ts reload: this long, the clip scrubbed through on an ease (spin), coming in and going over its ends.</summary>
        const float ReloadSeconds = 0.6f, ReloadEnds = 0.12f;

        /// <summary>
        /// A rifle's reload (unitViews.ts reload, syntyFigures.ts direct): «Reloading» over the upper body scrubbed through
        /// in 0.6 s on an ease, coming in and going over its ends (0.12 of it), the gun held fast in the right hand
        /// meanwhile. The pistol has none (0.10.1: its rest or its aim as they were).
        /// </summary>
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
