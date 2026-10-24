#nullable enable
// appearance.ts: what a soldier looks like (the barracks), and the look they start with.
// HEAD_IDS, BUILD_IDS and ARMOR_PART_IDS (Object.keys of HEADS, BUILDS and ARMOR_PARTS) are HeadIds.All, BuildIds.All
// and ArmorParts.All, in the same order; ARMOR_PARTS is ArmorPartDefs here, ArmorParts being the ids' class.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bastion.Engine
{
    /// <summary>An element of HEADS: `{ id: HeadId; name: string; paint: 'armour' | 'trim' | null }`.</summary>
    public sealed class HeadDef
    {
        public string Id { get; set; } = ""; // HeadIds
        public string Name { get; set; } = "";
        /// <summary>'armour', 'trim' or null.</summary>
        public string? Paint { get; set; }
    }

    /// <summary>An element of ARMOR_PARTS: `{ id: ArmorPart; name: string }`.</summary>
    public sealed class ArmorPartDef
    {
        public string Id { get; set; } = ""; // ArmorParts
        public string Name { get; set; } = "";
    }

    /// <summary>An element of BUILDS: `{ id: BuildId; name: string }`.</summary>
    public sealed class BuildDef
    {
        public string Id { get; set; } = ""; // BuildIds
        public string Name { get; set; } = "";
    }

    public static class Appearances
    {
        /// <summary>
        /// Head pieces. `paint` is what the headgear is painted with until it gets a
        /// colour of its own (`helmet`): the armour (`primary`), the trim
        /// (`secondary`), or nothing (a mask, a bare head).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, HeadDef> Heads = new Dictionary<string, HeadDef>
        {
            [HeadIds.Helmet] = new HeadDef { Id = HeadIds.Helmet, Name = "Casco táctico", Paint = "armour" },
            [HeadIds.Hood] = new HeadDef { Id = HeadIds.Hood, Name = "Capucha", Paint = "trim" },
            [HeadIds.Beret] = new HeadDef { Id = HeadIds.Beret, Name = "Boina", Paint = "trim" },
            [HeadIds.Cap] = new HeadDef { Id = HeadIds.Cap, Name = "Gorra", Paint = "trim" },
            [HeadIds.Mask] = new HeadDef { Id = HeadIds.Mask, Name = "Máscara", Paint = null },
            [HeadIds.Bare] = new HeadDef { Id = HeadIds.Bare, Name = "A cara descubierta", Paint = null },
        };

        /// <summary>Armour pieces with a colour of their own (barracks), head to feet.</summary>
        /// <remarks>ARMOR_PARTS: ArmorPartDefs here, ArmorParts being the ids' class.</remarks>
        public static readonly IReadOnlyDictionary<string, ArmorPartDef> ArmorPartDefs = new Dictionary<string, ArmorPartDef>
        {
            [ArmorParts.Helmet] = new ArmorPartDef { Id = ArmorParts.Helmet, Name = "Casco" },
            [ArmorParts.Torso] = new ArmorPartDef { Id = ArmorParts.Torso, Name = "Torso" },
            [ArmorParts.Arms] = new ArmorPartDef { Id = ArmorParts.Arms, Name = "Brazos" },
            [ArmorParts.Legs] = new ArmorPartDef { Id = ArmorParts.Legs, Name = "Piernas" },
        };

        public static readonly IReadOnlyDictionary<string, BuildDef> Builds = new Dictionary<string, BuildDef>
        {
            [BuildIds.Slim] = new BuildDef { Id = BuildIds.Slim, Name = "Delgada" },
            [BuildIds.Normal] = new BuildDef { Id = BuildIds.Normal, Name = "Normal" },
            [BuildIds.Heavy] = new BuildDef { Id = BuildIds.Heavy, Name = "Robusta" },
        };

        /// <summary>Armour swatches offered in the barracks (any `#rrggbb` is accepted).</summary>
        public static readonly IReadOnlyList<string> ArmorColors = new[]
        {
            "#a678ff", "#ffa630", "#56605a", "#2f3a44", "#6b7d5a", "#8a7a5c",
            "#c9ccd1", "#1d2124", "#8f2f2f", "#2f6b4f", "#3fa9ff", "#c9a13a",
        };

        public static readonly IReadOnlyList<string> VisorColors = new[] { "#9fe3ff", "#7dffb0", "#ffd27a", "#ff6a5a", "#d79fff", "#ffffff" };

        public const int NameMax = 20;
        public const int NicknameMax = 16;

        // /^#[0-9a-f]{6}$/i: `$` without the m flag is the very end in JavaScript (\z; .NET's $ also takes a last \n),
        // and `[0-9a-f]` with i is `[0-9a-fA-F]` (nothing else folds to those letters).
        private static readonly Regex Hex = new Regex("^#[0-9a-fA-F]{6}\\z", RegexOptions.CultureInvariant);

        /// <summary>A survivor model's id: the list lives in the client (Synty), the engine only checks its shape.</summary>
        /// <remarks>/^[a-z][a-zA-Z0-9]{0,23}$/, with JavaScript's `$` (\z).</remarks>
        public static readonly Regex CharacterId = new Regex("^[a-z][a-zA-Z0-9]{0,23}\\z", RegexOptions.CultureInvariant);

        /// <summary>`HEX.test(s)`: undefined (null) tests as «undefined», which is no colour.</summary>
        private static bool IsHex(string? s)
        {
            return s != null && Hex.IsMatch(s);
        }

        /// <summary>`a[part]`: the colour of one of the armour's pieces, if it has its own.</summary>
        public static string? PartOf(Appearance a, string part)
        {
            switch (part)
            {
                case ArmorParts.Helmet: return a.Helmet;
                case ArmorParts.Torso: return a.Torso;
                case ArmorParts.Arms: return a.Arms;
                case ArmorParts.Legs: return a.Legs;
                default: throw new ArgumentException($"Not an armour piece: {part}", nameof(part));
            }
        }

        /// <summary>`a[part] = color`.</summary>
        private static void SetPart(Appearance a, string part, string? color)
        {
            switch (part)
            {
                case ArmorParts.Helmet: a.Helmet = color; break;
                case ArmorParts.Torso: a.Torso = color; break;
                case ArmorParts.Arms: a.Arms = color; break;
                case ArmorParts.Legs: a.Legs = color; break;
                default: throw new ArgumentException($"Not an armour piece: {part}", nameof(part));
            }
        }

        public static bool IsAppearance(Appearance a)
        {
            return (
                IsHex(a.Primary) &&
                IsHex(a.Secondary) &&
                IsHex(a.Visor) &&
                HeadIds.All.Contains(a.Head) &&
                BuildIds.All.Contains(a.Build) &&
                (a.Sex == null || a.Sex == Sexes.F || a.Sex == Sexes.M) &&
                ArmorParts.All.All((part) => PartOf(a, part) == null || IsHex(PartOf(a, part))) &&
                (a.Character == null || CharacterId.IsMatch(a.Character))
            );
        }

        /// <summary>
        /// The colour a piece is painted in: its own, or else what it wore before
        /// pieces could be painted — the armour colour, or the trim for a beret, cap
        /// or hood.
        /// </summary>
        public static string PartColor(Appearance a, string part)
        {
            return PartOf(a, part) ?? (part == ArmorParts.Helmet && Heads[a.Head].Paint == "trim" ? a.Secondary : a.Primary);
        }

        /// <summary>
        /// A clean copy to store: the known fields only, in the order defaultAppearance
        /// writes them, and a piece only where it differs from what it would follow,
        /// so an unchanged look still compares equal to the default one.
        /// </summary>
        public static Appearance NormalizeAppearance(Appearance a)
        {
            var @out = new Appearance { Primary = a.Primary, Secondary = a.Secondary, Visor = a.Visor, Head = a.Head, Build = a.Build };
            if (!string.IsNullOrEmpty(a.Sex)) @out.Sex = a.Sex;
            foreach (var part in ArmorParts.All)
            {
                var own = PartOf(a, part);
                // toLowerCase: ToLowerInvariant (the same on colours; JavaScript's own rules differ only on a few letters, Σ at a word's end).
                if (own != null && own.ToLowerInvariant() != PartColor(@out, part).ToLowerInvariant()) SetPart(@out, part, own);
            }
            if (!string.IsNullOrEmpty(a.Character)) @out.Character = a.Character;
            return @out;
        }

        // `**` with a non-integer exponent: Math.Pow (the C library's) and V8's differ in the last bit now and then (in about one
        // of ten, over every channel value). The colours come out the same: each channel is rounded to 0–255 far from a half
        // (3.5e-4 at the nearest, over every `#rrggbb`), so a last bit never moves one.
        private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        private static double ToSrgb(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

        /// <summary>Mixes two `#rrggbb` colours in linear space, like THREE.Color.lerp; `t` = 0 is `a`, 1 is `b`.</summary>
        private static string Mix(string a, string b, double t)
        {
            double Channel(string hex, int i) => ToLinear(ParseHex(Slice(hex, 1 + i * 2, 3 + i * 2)) / 255);
            var @out = new[] { 0, 1, 2 }.Select((i) => Js.Round(ToSrgb(Channel(a, i) + (Channel(b, i) - Channel(a, i)) * t) * 255)).ToList();
            return "#" + string.Concat(@out.Select((v) => PadStart2(HexOf(v))));
        }

        /// <summary>String.prototype.slice(start, end) with both in range or past it (never negative here).</summary>
        private static string Slice(string s, int start, int end)
        {
            var from = Math.Min(start, s.Length);
            var to = Math.Min(end, s.Length);
            return to > from ? s.Substring(from, to - from) : "";
        }

        /// <summary>
        /// parseInt(s, 16): leading white space, a sign, an optional 0x, then as many hex digits as there are; NaN with
        /// none.
        /// </summary>
        private static double ParseHex(string s)
        {
            var i = 0;
            while (i < s.Length && Js.IsSpace(s[i])) i++;
            var sign = 1.0;
            if (i < s.Length && (s[i] == '+' || s[i] == '-'))
            {
                if (s[i] == '-') sign = -1;
                i++;
            }
            if (i + 1 < s.Length && s[i] == '0' && (s[i + 1] == 'x' || s[i + 1] == 'X')) i += 2;
            var value = 0.0;
            var digits = 0;
            for (; i < s.Length; i++)
            {
                var c = s[i];
                var d = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
                if (d < 0) break;
                value = value * 16 + d;
                digits++;
            }
            return digits == 0 ? double.NaN : sign * value;
        }

        /// <summary>Number.prototype.toString(16) of a whole number (Math.round's), or NaN.</summary>
        private static string HexOf(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (v < 0) return "-" + HexOf(-v);
            return ((long)v).ToString("x");
        }

        /// <summary>padStart(2, '0').</summary>
        private static string PadStart2(string s)
        {
            return s.Length < 2 ? new string('0', 2 - s.Length) + s : s;
        }

        /// <summary>What a soldier starts with on the head (docs/SIDEKICK.md): mostly bare or a cap, now and then a hood, a beret, a helmet or a mask.</summary>
        private static readonly IReadOnlyList<string> StartHeads = new[]
        {
            HeadIds.Bare, HeadIds.Bare, HeadIds.Bare, HeadIds.Cap, HeadIds.Cap, HeadIds.Hood, HeadIds.Beret, HeadIds.Helmet, HeadIds.Mask,
        };

        /// <summary>FNV-1a: the same number for the same soldier, everywhere.</summary>
        private static uint HashOf(string s)
        {
            // Math.imul(h ^ c, 16777619) and `>>> 0`: the same 32 bits, unsigned.
            var h = 2166136261u;
            for (var i = 0; i < s.Length; i++) h = unchecked((h ^ s[i]) * 16777619u);
            return h;
        }

        /// <summary>
        /// The look soldiers have before customization: their player's colour (and their sex, when known). No classes any
        /// more (docs/FASE9.md). With `seed` (the soldier's id), its own head and build, so that a squad of Sidekick figures
        /// isn't eight helmets alike (docs/SIDEKICK.md); without, the look of before, all helmets of a normal build, which
        /// migrateCampaign recognises.
        /// </summary>
        /// <remarks>`accent = PLAYER_COLORS[owner]`: a null accent is the player's colour.</remarks>
        public static Appearance DefaultAppearance(int owner, string? accent = null, string? sex = null, string? seed = null)
        {
            var color = accent ?? Content.PlayerColors[owner];
            uint? h = seed == null ? (uint?)null : HashOf(seed);
            var @out = new Appearance
            {
                Primary = Mix(color, "#56605a", 0.55),
                Secondary = color,
                Visor = "#9fe3ff",
                Head = h == null ? HeadIds.Helmet : StartHeads[(int)(h.Value % (uint)StartHeads.Count)],
                Build = h == null ? BuildIds.Normal : BuildIds.All[(int)((h.Value >> 8) % (uint)BuildIds.All.Count)],
            };
            if (!string.IsNullOrEmpty(sex)) @out.Sex = sex;
            return @out;
        }

        /// <summary>A random look, like XCOM's "randomize" button.</summary>
        public static Appearance RandomAppearance(Rng rng)
        {
            return new Appearance
            {
                Primary = rng.Pick(ArmorColors),
                Secondary = rng.Pick(ArmorColors),
                Visor = rng.Pick(VisorColors),
                Head = rng.Pick(HeadIds.All),
                Build = rng.Pick(BuildIds.All),
            };
        }

        /// <summary>"Clara «Halcón» Bosch": the nickname goes after the first name, XCOM-style.</summary>
        public static string DisplayName(string name, string? nickname = null)
        {
            if (string.IsNullOrEmpty(nickname)) return name;
            var space = name.IndexOf(' ');
            return space < 0 ? $"{name} «{nickname}»" : $"{name.Substring(0, space)} «{nickname}» {name.Substring(space + 1)}";
        }
    }
}
