using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------ gear names

        /// <summary>A gear's words: one, many, and the one with its article.</summary>
        private sealed class GearWords
        {
            public GearWords(string one, string many, string the)
            {
                One = one;
                Many = many;
                The = the;
            }

            public string One { get; }
            public string Many { get; }
            public string The { get; }
        }

        /// <summary>
        /// The words of what piles (Content.PileOf above 1): the belt's and the mods by their type, the magazines and the
        /// medicine by their kind. Content.Items names the belt's as they went before the runs, two to a place («Granadas»,
        /// «Cócteles molotov»); a gear is one now.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, GearWords> PiledGear = new Dictionary<string, GearWords>
        {
            [ItemIds.FirstAid] = new GearWords("botiquín", "botiquines", "el botiquín"),
            [ItemIds.FragGrenade] = new GearWords("granada", "granadas", "la granada"),
            [ItemIds.SmokeGrenade] = new GearWords("granada de humo", "granadas de humo", "la granada de humo"),
            [ItemIds.Flashbang] = new GearWords("granada aturdidora", "granadas aturdidoras", "la granada aturdidora"),
            [ItemIds.Molotov] = new GearWords("cóctel molotov", "cócteles molotov", "el cóctel molotov"),
            [ItemIds.ApRounds] = new GearWords("caja de munición perforante", "cajas de munición perforante", "la caja de munición perforante"),
            [ItemIds.TracerRounds] = new GearWords("caja de munición trazadora", "cajas de munición trazadora", "la caja de munición trazadora"),
            [ItemIds.MindShield] = new GearWords("tapones", "pares de tapones", "los tapones"),
            [WeaponModIds.Scope] = new GearWords("mira", "miras", "la mira"),
            [WeaponModIds.Stock] = new GearWords("culata", "culatas", "la culata"),
            [WeaponModIds.ExtMag] = new GearWords("cargador ampliado", "cargadores ampliados", "el cargador ampliado"),
            [WeaponModIds.Silencer] = new GearWords("silenciador", "silenciadores", "el silenciador"),
            [GearKinds.Mags] = new GearWords("cargador", "cargadores", "el cargador"),
            [GearKinds.Medicine] = new GearWords("medicina", "medicinas", "la medicina"),
        };

        /// <summary>
        /// A gear's name as a label (UI's sheets and lists, the Trueque, the Sucesos): «Granada», «2 granadas», «Fusil de
        /// cerrojo», «Chaleco con placas». One goes with its capital and no figure; more, with how many. The supplies are
        /// «Suministros» however many: their figure goes apart («Suministros · 35 de 100»).
        /// </summary>
        public static string GearName(string kind, string? type, int count = 1)
        {
            if (kind == GearKinds.Supplies) return "Suministros";
            var words = WordsOf(kind, type);
            return count > 1 ? $"{count} {words.Many}" : Capital(words.One);
        }

        /// <summary>A gear inside a sentence, with its article: «la granada», «2 granadas», «el fusil de cerrojo», «35 §» (one, «los suministros»).</summary>
        public static string GearThe(string kind, string? type, int count = 1)
        {
            if (kind == GearKinds.Supplies) return count > 1 ? $"{count} §" : "los suministros";
            var words = WordsOf(kind, type);
            return count > 1 ? $"{count} {words.Many}" : words.The;
        }

        /// <summary>
        /// The words of a gear: those of what piles, or a weapon's and a vest's from Content (one to a place, never more:
        /// its «many» is only the name again). A weapon is feminine when Content finds «una» of it.
        /// </summary>
        private static GearWords WordsOf(string kind, string? type)
        {
            var key = kind == GearKinds.Item || kind == GearKinds.Mod ? type ?? "" : kind;
            if (PiledGear.TryGetValue(key, out var piled)) return piled;
            if (kind == GearKinds.Weapon && type != null)
            {
                var def = type == FoundWeaponIds.Knife ? Content.Knife : Content.FoundWeapons.TryGetValue(type, out var found) ? found : null;
                if (def != null)
                {
                    var one = LowerFirst(def.Name);
                    return new GearWords(one, one, (def.Find.StartsWith("una ") ? "la " : "el ") + one);
                }
            }
            if (kind == GearKinds.Vest && type != null && Content.Vests.TryGetValue(type, out var vest))
            {
                var one = LowerFirst(vest.Name);
                return new GearWords(one, one, "el " + one);
            }
            if (type != null && Content.Items.TryGetValue(type, out var item))
            {
                var one = LowerFirst(item.Name);
                return new GearWords(one, one, one);
            }
            return new GearWords(type ?? kind, type ?? kind, type ?? kind);
        }
    }
}
