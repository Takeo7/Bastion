using Bastion.Engine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The state words that agree with the unit (Lore, 26/10; as ILESA and CAÍDA already did): a woman of the squad, or a
    /// hostage the lore names as one (Lore.NamedSex: the VIPs and the errands' people, who carry no appearance), takes
    /// CONTROLADA, PROTEGIDA, ATURDIDA, DESORIENTADA, SUPRIMIDA, CAUTIVA, EVACUADA; the dead and the enemies stay as
    /// they are («el zombi»).
    /// </summary>
    public static class UnitWords
    {
        public static bool Female(Unit u)
        {
            if (u == null) return false;
            if (u.Appearance != null) return u.Appearance.Sex == "f";
            return u.Name != null && Lore.NamedSex.TryGetValue(u.Name, out var sex) && sex == "f";
        }

        /// <summary>`word` (masculine, ending in o) as it agrees with `u`: «ATURDIDO» → «ATURDIDA», «aturdido» → «aturdida».</summary>
        public static string Agree(Unit u, string word)
        {
            if (!Female(u) || string.IsNullOrEmpty(word)) return word;
            var last = word[word.Length - 1];
            if (last == 'O') return word.Substring(0, word.Length - 1) + "A";
            if (last == 'o') return word.Substring(0, word.Length - 1) + "a";
            return word;
        }
    }
}
