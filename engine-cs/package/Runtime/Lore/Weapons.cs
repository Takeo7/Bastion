using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ----------------------------------------------------------------- weapons

        /// <summary>
        /// A weapon's card in the Armería (aim.ts weaponProfile; Arturo, 17/10: «para
        /// saber qué elegir»): the label of each figure, short.
        /// </summary>
        public static class WeaponLabels
        {
            public const string Damage = "Daño";
            public const string CritDamage = "Daño crítico";
            public const string Crit = "Crítico";
            public const string Aim = "Puntería";
            public const string Clip = "Cargador";
            public const string Actions = "Acciones por disparo";
            public const string ActionsMelee = "Acciones por golpe";
            public const string Noise = "Ruido";
            public const string Shred = "Quita blindaje";
            public const string Reach = "Alcance";
            public const string Range = "Puntería según la distancia";
        }

        /// <summary>A reach's name and what the weapon is good at.</summary>
        public sealed class Reach
        {
            public Reach(string name, string line)
            {
                Name = name;
                Line = line;
            }

            public string Name { get; }
            public string Line { get; }
        }

        /// <summary>
        /// Each reach, its name and what the weapon is good at, as its range table
        /// says (content.ts RANGE_TABLES): the shotgun adds most close and takes away
        /// from twelve tiles on, the rifles add close and never take away, the
        /// precision rifle takes away close and nothing from eleven on. By RangeTable:
        /// "short", "medium", "long", "none".
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Reach> Reaches = new Dictionary<string, Reach>
        {
            ["short"] = new Reach("Corto", "De cerca. Cuanto más cerca, más acierta; desde doce casillas, resta puntería."),
            ["medium"] = new Reach("Medio", "Vale para todo: suma de cerca y no resta de lejos."),
            ["long"] = new Reach("Largo", "De lejos. De cerca resta mucha puntería; desde once casillas, ya no."),
            ["none"] = new Reach("Cuerpo a cuerpo", "Contra quien está al lado. Casi no se oye."),
        };

        /// <summary>A figure of the card, in words: «4 balas», «2 acciones», «12 casillas»; the blade «No gasta». `what`: "clip", "actions" or "noise".</summary>
        public static string WeaponFigure(string what, int n, bool melee = false)
        {
            if (what == "clip") return melee || n == 0 ? "No gasta" : $"{n} {(n == 1 ? "bala" : "balas")}";
            if (what == "actions") return $"{n} {(n == 1 ? "acción" : "acciones")}";
            return $"{n} {(n == 1 ? "casilla" : "casillas")}";
        }
    }
}
