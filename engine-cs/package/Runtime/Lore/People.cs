using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ people

        /// <summary>The sex of every named person who can be on the battlefield or join the squad, so their model matches their name ("f" or "m").</summary>
        public static readonly IReadOnlyDictionary<string, string> NamedSex = new Dictionary<string, string>
        {
            ["Dra. Amara Nwosu"] = "f",
            ["Simão Ferreira"] = "m",
            ["Noor Haddad"] = "f",
            ["Celia Marsh"] = "f",
            ["Vera Lindqvist"] = "f",
            ["Lola Haskins"] = "f",
            ["Mei Takahara"] = "f",
            ["Otto Brandt"] = "m",
            ["Tomasz Rybak"] = "m",
            ["Dra. Irina Sokolova"] = "f",
            ["Ing. Kenji Mori"] = "m",
            ["Alcaldesa Mara Velasco"] = "f",
            ["Dr. Femi Adebayo"] = "m",
            ["Agente Claire Dubois"] = "f",
        };

        // ---------------------------------------------------------------- outfits

        /// <summary>
        /// A soldier's clothes in Personalizar (docs/SIDEKICK.md §6), by Arte's outfit
        /// id: what it is, never a trade (no «Cazador»: that is the Cuervo with the net)
        /// and never gendered, though some are worn by one sex only.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> OutfitNames = new Dictionary<string, string>
        {
            ["survivor1"] = "Chaleco de cuero",
            ["survivor2"] = "Chaleco verde",
            ["survivor3"] = "Camuflaje y cartuchos",
            ["survivor4"] = "Chaqueta marrón",
            ["survivor5"] = "Mangas remendadas",
            ["survivor6"] = "Cazadora de borrego",
            ["survivor7"] = "Chaqueta oscura",
            ["survivor8"] = "Verde oliva",
            ["civilian1"] = "Jersey amarillo",
            ["civilian2"] = "Chaleco acolchado",
            ["civilian3"] = "Ropa de calle",
            ["civilian4"] = "Chaleco vaquero",
            ["police1"] = "Equipo de asalto",
            ["police2"] = "Chaleco azul",
            ["police3"] = "Chaqueta de policía",
        };
    }
}
