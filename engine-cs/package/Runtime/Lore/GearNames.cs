using System.Collections.Generic;
using System.Linq;

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
        /// The weapons' and the vests' many, by their type (U19, 18/11: two of one found together go as one entry, «2 pistolas»):
        /// the noun and what agrees with it, «escopetas recortadas»; «antibalas», «de asalto», «con mira» as they are.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> GearPlurals = new Dictionary<string, string>
        {
            [FoundWeaponIds.Knife] = "cuchillos",
            [FoundWeaponIds.Pistol] = "pistolas",
            [FoundWeaponIds.AssaultRifle] = "fusiles de asalto",
            [FoundWeaponIds.PumpShotgun] = "escopetas",
            [FoundWeaponIds.PrecisionRifle] = "fusiles de precisión",
            [FoundWeaponIds.MachineGun] = "ametralladoras",
            [FoundWeaponIds.HuntingRifle] = "fusiles de cerrojo",
            [FoundWeaponIds.SawnOff] = "escopetas recortadas",
            [FoundWeaponIds.ScopedRifle] = "fusiles con mira",
            [FoundWeaponIds.LightMachineGun] = "ametralladoras ligeras",
            [FoundWeaponIds.Machete] = "machetes",
            [FoundWeaponIds.Pan] = "sartenes",
            [FoundWeaponIds.Bat] = "bates",
            [FoundWeaponIds.Crowbar] = "palancas",
            [FoundWeaponIds.Chainsaw] = "motosierras",
            [FoundWeaponIds.Katana] = "katanas",
            [FoundWeaponIds.Longsword] = "espadas",
            [FoundWeaponIds.Rake] = "rastrillos",
            [FoundWeaponIds.Stick] = "palos",
            [VestIds.Vest] = "chalecos antibalas",
            [VestIds.HeavyVest] = "chalecos con placas",
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
        /// A gear brought into a sentence for the first time («Anja encuentra…»), with «un»/«una»: «una granada», «un fusil de
        /// cerrojo», «unos tapones», «2 granadas». The supplies, with their figure: «117 §».
        /// </summary>
        public static string GearA(string kind, string? type, int count = 1)
        {
            if (kind == GearKinds.Supplies) return $"{count} §";
            var words = WordsOf(kind, type);
            if (count > 1) return $"{count} {words.Many}";
            var the = words.The;
            if (the.StartsWith("el ")) return "un " + the.Substring(3);
            if (the.StartsWith("la ")) return "una " + the.Substring(3);
            if (the.StartsWith("los ")) return "unos " + the.Substring(4);
            if (the.StartsWith("las ")) return "unas " + the.Substring(4);
            return the;
        }

        /// <summary>
        /// The words of a gear: those of what piles, or a weapon's and a vest's from Content, their many from GearPlurals (one
        /// to a place, but two found together are told as one: U19). A weapon is feminine when Content finds «una» of it.
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
                    return new GearWords(one, GearPlurals.TryGetValue(type, out var many) ? many : one, (def.Find.StartsWith("una ") ? "la " : "el ") + one);
                }
            }
            if (kind == GearKinds.Vest && type != null && Content.Vests.TryGetValue(type, out var vest))
            {
                var one = LowerFirst(vest.Name);
                return new GearWords(one, GearPlurals.TryGetValue(type, out var many) ? many : one, "el " + one);
            }
            if (type != null && Content.Items.TryGetValue(type, out var item))
            {
                var one = LowerFirst(item.Name);
                return new GearWords(one, one, one);
            }
            return new GearWords(type ?? kind, type ?? kind, type ?? kind);
        }

        /// <summary>
        /// What a belt item does, in one line, for its sheet on hover in an expedition (Arturo, 18/11, T4): its uses in a
        /// fight (ItemDef.Charges: each piece on the belt gives them, Gameplay 18/11), what it does, and that it is used from
        /// the belt, not the pack. Null for the old armoury's others (ItemDef.Description has theirs).
        /// </summary>
        public static string? ItemLine(string item)
        {
            string what;
            var thrown = true;
            switch (item)
            {
                case ItemIds.FragGrenade:
                    what = "Daña a todos en el área y abre boquetes en las paredes.";
                    break;
                case ItemIds.Molotov:
                    what = "Donde cae, arde un cuadrado de 3×3 casillas durante 2 turnos.";
                    break;
                case ItemIds.SmokeGrenade:
                    what = "Humo durante 2 turnos: −20 a la puntería contra quien esté dentro.";
                    break;
                case ItemIds.Flashbang:
                    what = "Desorienta a los enemigos de la zona: −20 de puntería. No hace daño.";
                    break;
                case ItemIds.FirstAid:
                    what = "Cura 4 de salud a uno mismo o a un compañero de al lado.";
                    thrown = false;
                    break;
                default:
                    return null;
            }
            var n = Content.Items.TryGetValue(item, out var def) && def.Charges != null ? def.Charges.Values.Sum() : 1;
            var uses = thrown ? (n == 1 ? "Un lanzamiento" : $"{UsesWord(n)} lanzamientos") : (n == 1 ? "Un uso" : $"{UsesWord(n)} usos");
            return $"{uses}. {what} {(thrown ? "Se lanza" : "Se usa")} desde el cinturón.";
        }

        /// <summary>«Dos», «Tres»… at the head of ItemLine; past five, the figure.</summary>
        private static string UsesWord(int n)
        {
            return n >= 2 && n <= 5 ? new[] { "Dos", "Tres", "Cuatro", "Cinco" }[n - 2] : n.ToString();
        }
    }
}
