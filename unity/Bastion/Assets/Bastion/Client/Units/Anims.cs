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
            public Dictionary<string, Death[]> deaths = new Dictionary<string, Death[]>();
            public Dictionary<string, Hit[]> hits = new Dictionary<string, Hit[]>();
            public string[] actions = Array.Empty<string>();
            public Dictionary<string, Dictionary<string, string>> armedMotions = new Dictionary<string, Dictionary<string, string>>();
            public string[] rifleRests = Array.Empty<string>();
            public Dictionary<string, object[]> feedClips = new Dictionary<string, object[]>();
            public Dictionary<string, WeaponArt> weapons = new Dictionary<string, WeaponArt>();
            public Dictionary<string, string[]> unitWeapons = new Dictionary<string, string[]>();
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

        public static IReadOnlyList<Death> Deaths(string build) => T.deaths.TryGetValue(build, out var d) ? d : Array.Empty<Death>();

        public static IReadOnlyList<Hit> Hits(string build) => T.hits.TryGetValue(build, out var h) ? h : Array.Empty<Hit>();

        public static IReadOnlyList<string> IdleVariations(string build) => T.idleVariations.TryGetValue(build, out var v) ? v : Array.Empty<string>();

        /// <summary>A soldier moving with a gun in the hands ("rifle", "pistol"): the motion's clip of its own, "own" (Synty's), or null.</summary>
        public static string Armed(string kind, string motion) => T.armedMotions.TryGetValue(kind, out var m) && m.TryGetValue(motion, out var id) ? id : null;

        public static IReadOnlyList<string> RifleRests => T.rifleRests;

        public static WeaponArt Weapon(string id) => id != null && T.weapons.TryGetValue(id, out var w) ? w : null;

        public static IEnumerable<WeaponArt> Weapons => T.weapons.Values;

        /// <summary>The weapon a template carries at a weapon tier (1–3; syntyManifest.ts UNIT_WEAPONS); null, none.</summary>
        public static string UnitWeapon(string template, int? tier) =>
            template != null && T.unitWeapons.TryGetValue(template, out var w) && w.Length > 0 ? w[Mathf.Clamp((tier ?? 1) - 1, 0, w.Length - 1)] : null;

        /// <summary>A part of the dead's feeding ("bite", "down", "loop", "up"): its clip and the seconds of it played.</summary>
        public static (string id, float from, float to)? Feed(string part) =>
            T.feedClips.TryGetValue(part, out var f) && f.Length >= 3 ? ((string)f[0], Convert.ToSingle(f[1]), Convert.ToSingle(f[2])) : ((string, float, float)?)null;
    }
}
