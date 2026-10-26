using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>A way of dying (syntyManifest.ts Death): `falls`, the way the body goes, in its own frame (0 ahead, π/2 to its left).</summary>
    public sealed class Death
    {
        public string id;
        public float falls;
        public float reach;
        public bool head;
        public bool close;
    }

    /// <summary>A hit taken (syntyManifest.ts Hit): `from`, the way the blow comes from in the body's frame; `hard`, a critical's.</summary>
    public sealed class Hit
    {
        public string id;
        public float from;
        public bool hard;
        public bool head;
        public bool close;
    }

    /// <summary>
    /// A gun or a blade (syntyManifest.ts WEAPONS), in its model's frame: the barrel along +z, the grip at the origin.
    /// `fore`: where the support hand holds a two-handed gun ([height, length]); `muzzle` where it is not the far end.
    /// </summary>
    public sealed class WeaponArt
    {
        public string fbx;
        public bool melee;
        public bool pistol;
        public float[] fore;
        public float size;
        public float sight;
        public bool reflex;
        public float[] muzzle;
    }

    /// <summary>
    /// Which clip plays each part, for each build ("masc", "femn"), as 0.10.1's syntyManifest.ts said
    /// (Resources/Anims.json, unity/tools/anims-json.mts); the clips themselves are FigureCatalog's.
    /// </summary>
    public static class Anims
    {
        /// <summary>The clips a figure blends as it stands and moves (syntyAnims.ts MOTION_ORDER).</summary>
        public static readonly string[] MotionOrder = { "idle", "crouchIdle", "walk", "run", "crouchWalk", "sprint" };

        sealed class Tables
        {
            public Dictionary<string, Dictionary<string, string>> motions = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, string> deadMotions = new Dictionary<string, string>();
            public Dictionary<string, string[]> idleVariations = new Dictionary<string, string[]>();
            public Dictionary<string, Dictionary<string, string>> stances = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, string>> gestures = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Death[]> deaths = new Dictionary<string, Death[]>();
            public Dictionary<string, Hit[]> hits = new Dictionary<string, Hit[]>();
            public string[] actions = Array.Empty<string>();
            public Dictionary<string, Dictionary<string, string>> armedMotions = new Dictionary<string, Dictionary<string, string>>();
            public string[] rifleRests = Array.Empty<string>();
            public Dictionary<string, object[]> feedClips = new Dictionary<string, object[]>();
            public Dictionary<string, WeaponArt> weapons = new Dictionary<string, WeaponArt>();
            public Dictionary<string, string[]> unitWeapons = new Dictionary<string, string[]>();
            public Dictionary<string, Dictionary<string, string>> campLoops = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, string[]> campVariations = new Dictionary<string, string[]>();
            public Dictionary<string, Dictionary<string, string>> campMoves = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, CampPart> campParts = new Dictionary<string, CampPart>();
            public Dictionary<string, float[]> campSpans = new Dictionary<string, float[]>();
            public string[] campFree = Array.Empty<string>();
            public Dictionary<string, string> campItems = new Dictionary<string, string>();
        }

        /// <summary>
        /// A clip that is more than one thing (syntyManifest.ts CAMP_PARTS: Mixamo's kneeling by a fire kneels, warms the
        /// hands, gets up): the seconds of the part stayed in (played back and forth: its ends meet only so) and of those into
        /// it and out of it (`id`In, `id`Out among the camp's).
        /// </summary>
        public sealed class CampPart
        {
            public float[] loop;
            public float[] into;
            public float[] @out;
        }

        static Tables tables;

        static Tables T
        {
            get
            {
                if (tables != null) return tables;
                var text = Resources.Load<TextAsset>("Anims") ?? throw new InvalidOperationException("Resources/Anims.json is missing");
                return tables = JsonConvert.DeserializeObject<Tables>(text.text);
            }
        }

        /// <summary>The clip a build stands or moves in (`dead`: the dead's walk, run and sprint, Goblin Locomotion's).</summary>
        public static string Motion(string build, string motion, bool dead) =>
            dead && T.deadMotions.TryGetValue(motion, out var d) ? d : T.motions.TryGetValue(build, out var m) && m.TryGetValue(motion, out var id) ? id : null;

        /// <summary>A way of standing in place of the idle (syntyManifest.ts STANCES: "drunk" the dead, "aggressive" the living enemy, "slumped" the wounded).</summary>
        public static string Stance(string build, string stance) => stance != null && T.stances.TryGetValue(build, out var s) && s.TryGetValue(stance, out var id) ? id : null;

        /// <summary>
        /// The camp's (clearingLife.ts, syntyManifest.ts CAMP_LOOPS): the clip a build stays in instead of the idle
        /// (sitChair, warmHands, leanLeft, drink…), an id or a part of one (CampPart).
        /// </summary>
        public static string CampLoop(string build, string loop) => loop != null && T.campLoops.TryGetValue(build, out var l) && l.TryGetValue(loop, out var id) ? id : null;

        /// <summary>The clips a build plays once now and then at the camp in the plain variations' place (CAMP_VARIATIONS: a stretch, a yawn).</summary>
        public static IReadOnlyList<string> CampVariations(string build) => T.campVariations.TryGetValue(build, out var v) ? v : Array.Empty<string>();

        /// <summary>A camp move's clip (CAMP_MOVES: sitting down and getting up, leaning, kneeling, a sip, into La Lata): an id, or `id`In / `id`Out of a CampPart.</summary>
        public static string CampMove(string build, string move) => move != null && T.campMoves.TryGetValue(build, out var m) && m.TryGetValue(move, out var id) ? id : null;

        /// <summary>Clip `id`'s parts, if it is more than one thing (CAMP_PARTS); null if not.</summary>
        public static CampPart CampPartOf(string id) => id != null && T.campParts.TryGetValue(id, out var p) ? p : null;

        /// <summary>The seconds of clip `id` the camp plays (CAMP_SPANS: into a car from at its door, a step up), or null for all of it.</summary>
        public static (float from, float to)? CampSpan(string id) => id != null && T.campSpans.TryGetValue(id, out var s) && s.Length >= 2 ? (s[0], s[1]) : ((float, float)?)null;

        /// <summary>Whether move `move`'s hips go as high as the clip has them, not kept on the feet (CAMP_FREE: into a car, up a step).</summary>
        public static bool CampFree(string move) => Array.IndexOf(T.campFree, move) >= 0;

        /// <summary>What is held drinking or eating at the camp (CAMP_ITEMS: drink a canteen's cup, eat a ration bar), a prop's id.</summary>
        public static string CampItem(string loop) => loop != null && T.campItems.TryGetValue(loop, out var id) ? id : null;

        /// <summary>A gesture made over the upper body (syntyManifest.ts GESTURES: point, nod, chin, shrug, calm).</summary>
        public static string Gesture(string build, string gesture) => gesture != null && T.gestures.TryGetValue(build, out var g) && g.TryGetValue(gesture, out var id) ? id : null;

        public static IReadOnlyList<Death> Deaths(string build) => T.deaths.TryGetValue(build, out var d) ? d : Array.Empty<Death>();

        public static IReadOnlyList<Hit> Hits(string build) => T.hits.TryGetValue(build, out var h) ? h : Array.Empty<Hit>();

        public static IReadOnlyList<string> IdleVariations(string build) => T.idleVariations.TryGetValue(build, out var v) ? v : Array.Empty<string>();

        /// <summary>A soldier moving with a gun in the hands ("rifle", "pistol"): the motion's clip of its own, "own" (Synty's), or null.</summary>
        public static string Armed(string kind, string motion) => T.armedMotions.TryGetValue(kind, out var m) && m.TryGetValue(motion, out var id) ? id : null;

        public static IReadOnlyList<string> RifleRests => T.rifleRests;

        public static WeaponArt Weapon(string id) => id != null && T.weapons.TryGetValue(id, out var w) ? w : null;

        public static IEnumerable<WeaponArt> Weapons => T.weapons.Values;

        /// <summary>
        /// The model of one of the Armería's weapons (FoundWeaponIds) at a weapon tier (syntyFigures.ts foundModel); null,
        /// none (no weapon in the hand: the pistol).
        /// </summary>
        public static string FoundModel(string kind, int tier = 1)
        {
            if (kind == null || !FoundModels.TryGetValue(kind, out var m)) return null;
            return m[Mathf.Clamp(tier - 1, 0, m.Length - 1)];
        }

        static readonly Dictionary<string, string[]> FoundModels = new Dictionary<string, string[]>
        {
            ["assaultRifle"] = new[] { "rifle", "rifle2", "rifle3" },
            ["pumpShotgun"] = new[] { "shotgun" },
            ["precisionRifle"] = new[] { "sniper", "sniper2", "sniper2" },
            ["machineGun"] = new[] { "machineGun" },
            ["machete"] = new[] { "machete" },
            ["huntingRifle"] = new[] { "boltRifle" },
            ["sawnOff"] = new[] { "shotgun" },
            ["scopedRifle"] = new[] { "sniper2" },
            ["lightMachineGun"] = new[] { "machineGun2" },
        };

        /// <summary>The weapon a template carries at a weapon tier (1–3; syntyManifest.ts UNIT_WEAPONS); null, none.</summary>
        public static string UnitWeapon(string template, int? tier) =>
            template != null && T.unitWeapons.TryGetValue(template, out var w) && w.Length > 0 ? w[Mathf.Clamp((tier ?? 1) - 1, 0, w.Length - 1)] : null;

        /// <summary>A part of the dead's feeding ("bite", "down", "loop", "up"): its clip and the seconds of it played.</summary>
        public static (string id, float from, float to)? Feed(string part) =>
            T.feedClips.TryGetValue(part, out var f) && f.Length >= 3 ? ((string)f[0], Convert.ToSingle(f[1]), Convert.ToSingle(f[2])) : ((string, float, float)?)null;
    }
}
