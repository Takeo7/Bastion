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

        /// <summary>
        /// What a hand weapon of the Armería does that the others don't, for its card (Ana's #5 of 2/11): the long blades
        /// strike everyone alongside (Content.Sweeps), the rake reaches two tiles (Content.ReachOf, Aim.InReachOf). Null for any
        /// other.
        /// </summary>
        public static string? MeleeLine(string foundWeapon)
        {
            switch (foundWeapon)
            {
                case FoundWeaponIds.Katana:
                case FoundWeaponIds.Longsword:
                case FoundWeaponIds.Chainsaw:
                    return "Con el Tajo, golpea a todos los enemigos que tenga al lado, no a uno.";
                case FoundWeaponIds.Rake:
                    return "Llega a dos casillas, en línea recta o en diagonal, si en medio no hay nadie.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// What a weapon is good for, in one line, for its sheet on hover (Arturo, 18/11, T4: «para saber cuál es mejor que
        /// otra»), by the weapon it fires as (WeaponIds): the figures go under it, by WeaponLabels. A long blade's sweep and
        /// the rake's reach are MeleeLine's. Null for the enemies' and the base's.
        /// </summary>
        public static string? WeaponLine(string weapon)
        {
            switch (weapon)
            {
                case WeaponIds.Rifle:
                    return "Para todo: suma puntería de cerca y no la pierde de lejos.";
                case WeaponIds.Shotgun:
                    return "Mucho daño de cerca. Desde doce casillas, pierde puntería.";
                case WeaponIds.Cannon:
                    return "Mucho daño, de cerca y de lejos, pero hace mucho ruido y el cargador es corto.";
                case WeaponIds.Sniper:
                    return "Para lejos: de cerca pierde mucha puntería. Cada disparo gasta las dos acciones.";
                case WeaponIds.Pistol:
                    return "Poco daño, pero es el arma de fuego que menos se oye.";
                case WeaponIds.Knife:
                    return "Con el que empiezan todos. Poco daño, y casi no se oye.";
                case WeaponIds.Sword:
                    return "Acierta mucho y casi no se oye.";
                case WeaponIds.Pan:
                    return "Acierta bastante, pero hace poco daño y el golpe se oye.";
                case WeaponIds.Bat:
                    return "Buen daño, y se oye poco.";
                case WeaponIds.Crowbar:
                    return "Cada golpe quita 1 de blindaje. Con ella se quitan las tablas de puertas y ventanas.";
                case WeaponIds.Chainsaw:
                    return "La que más daño hace, y la que más se oye.";
                case WeaponIds.Katana:
                    return "La mejor hoja: mucho daño, acierta mucho y casi no se oye.";
                case WeaponIds.Longsword:
                    return "Buen daño, y acierta bastante.";
                case WeaponIds.Rake:
                    return "Hace poco daño, pero llega más lejos.";
                case WeaponIds.Stick:
                    return "El que menos daño hace, pero casi nunca falla.";
                default:
                    return null;
            }
        }

        /// <summary>A figure of the card, in words: «4 balas», «2 acciones», «12 casillas»; the blade «No gasta». `what`: "clip", "actions" or "noise".</summary>
        public static string WeaponFigure(string what, int n, bool melee = false)
        {
            if (what == "clip") return melee || n == 0 ? "No gasta" : $"{n} {(n == 1 ? "bala" : "balas")}";
            if (what == "actions") return $"{n} {(n == 1 ? "acción" : "acciones")}";
            return $"{n} {(n == 1 ? "casilla" : "casillas")}";
        }
    }
}
