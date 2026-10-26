using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// Everything a soldier is made from (0.10.1's wardrobe.ts Dress): their outfit, what is on their head, their sex
    /// and build, the face, the hair and its colour, a beard, the skin. Of their look (Appearance) where it says, the rest
    /// picked by their seed (their campaign id; a skirmish soldier's name), the same everywhere.
    /// </summary>
    public sealed class Dress
    {
        public string outfit;
        /// <summary>"f" or "m".</summary>
        public string sex;
        /// <summary>"slim", "normal" or "heavy".</summary>
        public string build;
        public string face;
        public string hair;
        public string beard;
        public string headwear;
        /// <summary>SoldierPalette.SkinTones; −1 the dead's (a risen soldier).</summary>
        public int skin;
        public int hairColor;

        // 0.10.1's tables (syntyManifest.ts OUTFITS, HAIRS, FACES, BEARDS), in their order: `pick` goes by it.
        static readonly (string id, string sexes)[] Outfits =
        {
            ("survivor1", "fm"), ("survivor2", "mf"), ("survivor3", "fm"), ("survivor4", "mf"), ("survivor5", "mf"), ("survivor6", "mf"),
            ("survivor7", "mf"), ("survivor8", "fm"), ("civilian1", "fm"), ("civilian2", "mf"), ("civilian3", "mf"), ("civilian4", "fm"),
            ("police1", "mf"), ("police2", "fm"), ("police3", "mf"),
        };
        static readonly (string id, string sex)[] Hairs =
        {
            ("hairBun", "f"), ("hairBangs", "f"), ("hairSidePony", "f"), ("hairCurls", "f"), ("hairPony", "f"), ("hairLong", "f"), ("hairTopBun", "f"),
            ("hairPonyBangs", "f"), ("hairStraight", "f"), ("hairHighBun", "f"), ("hairSwept", "f"), ("hairUnderF", "f"), ("hairFade", "m"),
            ("hairMessy", "m"), ("hairMedium", "m"), ("hairBack", "m"), ("hairShaggy", "m"), ("hairLongM", "m"), ("hairUnderM", "m"),
        };
        static readonly string[] Faces = { "face1", "face2", "face3", "face4", "face5", "face6", "face7", "face8" };
        static readonly string[] Beards = { "beard1", "beard2", "beard3", "beard4", "beard5", "beard6", "beard7", "beard8" };
        /// <summary>The hair under each headwear (HEADWEAR.hair): HAIR_UNDER's, none (a hood) or the soldier's own (a mask).</summary>
        static readonly Dictionary<string, string> HairUnder = new Dictionary<string, string> { ["helmet"] = "under", ["hood"] = "none", ["beret"] = "under", ["cap"] = "under", ["mask"] = "own" };
        /// <summary>How likely each hair colour is (wardrobe.ts HAIR_ODDS): the dark ones most.</summary>
        static readonly int[] HairOdds = new[] { 4, 4, 3, 1, 1, 1, 1 }.SelectMany((n, i) => Enumerable.Repeat(i, n)).ToArray();

        /// <summary>
        /// wardrobe.ts dressOf. `sex` is who has no look's (0.10.1's sexOfPerson, by their name: the campaign isn't in C#
        /// yet, so none is given and it is picked by the seed); `risen`, a soldier come back as one of the dead.
        /// </summary>
        public static Dress Of(Appearance look, string seed, string sex = null, bool risen = false)
        {
            string s = SexOf(look, seed, sex);
            string outfit = Outfits.Any(o => o.id == look?.Character) ? look.Character
                : seed != null && rostered.TryGetValue(seed, out var worn) ? worn
                : Pick(Outfits.Where(o => o.sexes.Contains(s)).Select(o => o.id).ToArray(), seed + "|outfit");
            string head = !string.IsNullOrEmpty(look?.Head) ? look.Head : "helmet";
            string headwear = head == "bare" ? null : head;
            string own = Pick(Hairs.Where(h => h.sex == s && h.id != Under(s)).Select(h => h.id).ToArray(), seed + "|hair");
            string under = headwear != null && HairUnder.TryGetValue(headwear, out var u) ? u : "own";
            return new Dress
            {
                outfit = outfit,
                sex = s,
                build = !string.IsNullOrEmpty(look?.Build) ? look.Build : "normal",
                face = Pick(Faces, seed + "|face"),
                hair = under == "own" ? own : under == "under" ? Under(s) : null,
                // Half the men shaven.
                beard = s == "m" ? Pick(Beards.Concat(Beards.Select(_ => (string)null)).ToArray(), seed + "|beard") : null,
                headwear = headwear,
                skin = risen ? -1 : Pick(Enumerable.Range(0, SoldierPalette.SkinTones.Length).ToArray(), seed + "|skin"),
                hairColor = Pick(HairOdds, seed + "|hairColor"),
            };
        }

        /// <summary>
        /// The order recruits are dressed in when they haven't chosen (wardrobe.ts WEAR_ORDER), by sex: survivors the most,
        /// then the civilians' winter clothes, a policeman now and then; neighbours unlike each other.
        /// </summary>
        static readonly Dictionary<string, string[]> WearOrder = new Dictionary<string, string[]>
        {
            ["f"] = new[] { "survivor1", "survivor3", "survivor7", "survivor4", "civilian1", "survivor6", "police2", "survivor2", "civilian4", "survivor8", "survivor5", "civilian2", "police1", "civilian3", "police3" },
            ["m"] = new[] { "survivor2", "survivor4", "survivor8", "survivor5", "civilian3", "survivor6", "police1", "survivor3", "survivor7", "civilian2", "survivor1", "civilian4", "police3", "civilian1", "police2" },
        };

        /// <summary>The campaign's soldiers' outfits when they haven't chosen one (Roster), by campaign id.</summary>
        static readonly Dictionary<string, string> rostered = new Dictionary<string, string>();

        /// <summary>
        /// Dresses the campaign's soldiers (every one, the fallen too: wardrobe.ts setRoster, main.ts on each campaign
        /// state): in the order they joined, each the next outfit of their sex's WearOrder, so no two wear the same until
        /// all have been worn, and nobody's changes when others join or fall. Whether anyone's changed.
        /// </summary>
        public static bool Roster(IEnumerable<CampaignSoldier> soldiers)
        {
            var before = new Dictionary<string, string>(rostered);
            rostered.Clear();
            int Joined(string id)
            {
                var digits = new string((id ?? "").Where(char.IsDigit).ToArray());
                return digits.Length > 0 && long.TryParse(digits, out long n) && n <= int.MaxValue ? (int)n : 0;
            }
            var next = new Dictionary<string, int> { ["f"] = 0, ["m"] = 0 };
            foreach (var s in (soldiers ?? Enumerable.Empty<CampaignSoldier>()).OrderBy(x => Joined(x.Id)))
            {
                string sex = SexOf(s.Appearance, s.Id);
                var order = WearOrder[sex == "f" ? "f" : "m"];
                string key = sex == "f" ? "f" : "m";
                rostered[s.Id] = order[next[key]++ % order.Length];
            }
            return before.Count != rostered.Count || rostered.Any(kv => !before.TryGetValue(kv.Key, out var o) || o != kv.Value);
        }

        static string Under(string sex) => sex == "f" ? "hairUnderF" : "hairUnderM";

        /// <summary>
        /// The outfits someone of this sex ("f", "m") can wear, in the barracks' order (wardrobe.ts outfitsFor): Personalizar's
        /// Ropa, after «Al azar»; their names are Lore.OutfitNames.
        /// </summary>
        public static IReadOnlyList<string> OutfitsFor(string sex) => Outfits.Where(o => o.sexes.Contains(sex)).Select(o => o.id).ToList();

        /// <summary>The outfit a look names (Appearance.Character; wardrobe.ts outfitOf), or null: one by their id («Al azar»).</summary>
        public static string OutfitOf(string character) => Outfits.Any(o => o.id == character) ? character : null;

        /// <summary>wardrobe.ts soldierSex: their look's; else their outfit's if only one sex wears it; else picked by the seed.</summary>
        public static string SexOf(Appearance look, string seed, string sex = null)
        {
            string own = !string.IsNullOrEmpty(look?.Sex) ? look.Sex : sex;
            if (!string.IsNullOrEmpty(own)) return own;
            string sexes = Outfits.FirstOrDefault(o => o.id == look?.Character).sexes ?? "fm";
            return sexes.Length == 1 ? sexes : Pick(new[] { "f", "m" }, seed + "|sex");
        }

        /// <summary>The pieces it is made of (wardrobe.ts piecesOf), the outfit first: (piece kind, id).</summary>
        public IEnumerable<(string piece, string id)> Pieces()
        {
            yield return ("outfit", outfit);
            yield return ("face", face);
            if (hair != null) yield return ("hair", hair);
            if (beard != null) yield return ("beard", beard);
            if (headwear != null) yield return ("headwear", headwear);
        }

        /// <summary>The same soldier, the same model (wardrobe.ts dressKey).</summary>
        public string Key => string.Join("|", outfit, sex, build, face, hair, beard, headwear, skin, hairColor);

        /// <summary>wardrobe.ts pick: FNV-1a of the seed's UTF-16 code units, as JavaScript's 32-bit integers.</summary>
        static T Pick<T>(T[] list, string seed)
        {
            uint h = 0x811c9dc5;
            foreach (char c in seed) h = unchecked((h ^ c) * 0x01000193);
            return list[h % (uint)list.Length];
        }
    }
}
