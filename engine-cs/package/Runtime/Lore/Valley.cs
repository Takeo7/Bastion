using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ valley

        /// <summary>
        /// The places one knows the valley by, named small on its map under the zones'
        /// names (Arte places them; docs/LORE.md «El mundo»). Each says something of its
        /// zone: the Peaje is where the Cuervos charge Sappada its toll, the Emisora is
        /// the Voz's, the Pico Gris holds the repeater of chapter 4.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Landmarks = new Dictionary<string, string>
        {
            // The home: a clearing in the pines above Bielsa (the key is the old one, kept for the map).
            ["atalaya"] = "El Bastión",
            // Bielsa: the old station, burnt in the autumn (Arturo, 18/10: «Atalaya cayó»); just its name, the drawing says the rest.
            ["ruins"] = "Atalaya",
            // Bielsa: the village of the bells (quest «Las campanas de Tende»).
            ["telm"] = "Tende",
            // Rudnik: the Sindicato's mine.
            ["mine"] = "Mina",
            // El Pantano Gris: the reservoir's dam, its dead power plant and the peak with the repeater.
            ["dam"] = "Presa",
            ["plant"] = "Central",
            ["peak"] = "Pico Gris",
            // La Vega: the Bodegas' cellars.
            ["wineries"] = "Bodegas",
            // Sappada: the Cuervos' toll on the road.
            ["toll"] = "Peaje",
            // Tarbes: chapter 2.
            ["hospital"] = "Hospital",
            // Koper: where it began, and its port.
            ["institute"] = "Instituto Koper",
            ["port"] = "Puerto",
            // Los Llanos de Ceniza: the Voz's station, and the Camioneros' stop.
            ["station"] = "Emisora",
            ["truckStop"] = "Parada de camiones",
        };

        /// <summary>A zone's name after «a»: «al Pantano Gris», «a La Vega».</summary>
        public static string ToZone(string name)
        {
            return name.StartsWith("El ") ? $"al {name.Substring(3)}" : $"a {name}";
        }

        /// <summary>A zone's name after «de»: «del Pantano Gris», «de La Vega».</summary>
        public static string OfZone(string name)
        {
            return name.StartsWith("El ") ? $"del {name.Substring(3)}" : $"de {name}";
        }

        private static string JoinWith(IReadOnlyList<string> names, string last)
        {
            if (names.Count > 1) return $"{string.Join(", ", Slice(names, 0, names.Count - 1))} {last} {names[names.Count - 1]}";
            return names.Count == 1 ? names[0] : "";
        }

        /// <summary>The alert's three lines: its kicker, title and text.</summary>
        public sealed class Alert
        {
            public Alert(string kicker, string title, string text)
            {
                Kicker = kicker;
                Title = title;
                Text = text;
            }

            public string Kicker { get; }
            public string Title { get; }
            public string Text { get; }
        }

        /// <summary>
        /// The alert when the Marea reaches a zone or leaves it (docs/FASE5.md). Before
        /// the Bastion knows the Marea's name (PARTITURA_LEVEL) it is just «los muertos».
        /// Written without verbs that agree with the zone, which may be plural.
        /// </summary>
        public static Alert MareaAlert(int level, string zone, bool reached)
        {
            var kicker = DoomName(level);
            if (level >= PartituraLevel)
            {
                return reached
                    ? new Alert(kicker, $"La Marea llega {ToZone(zone)}", $"La Marea llega {ToZone(zone)}. Su enclave calla, y por sus caminos solo andan muertos: de allí ya no llegará nada.")
                    : new Alert(kicker, $"La Marea se retira {OfZone(zone)}", $"La Marea se retira {OfZone(zone)}. Vuelve a haber quien conteste por radio.");
            }
            return reached
                ? new Alert(kicker, $"Los muertos toman {zone}", $"Los muertos han tomado {zone}. Su enclave calla: de allí ya no llegará nada.")
                : new Alert(kicker, $"Los muertos se retiran {OfZone(zone)}", $"Los muertos se retiran {OfZone(zone)}. Vuelve a haber quien conteste por radio.");
        }

        /// <summary>Why a zone under the Marea can't be contacted.</summary>
        public static string FloodedContactReason(int level, string zone)
        {
            return level >= PartituraLevel ? $"La Marea cubre {zone}: nadie contesta hasta que se retire." : $"Los muertos han tomado {zone}: nadie contesta hasta que se retiren.";
        }

        /// <summary>Why a zone with no road from ours can't be contacted; `via` are the zones to contact first.</summary>
        public static string NoRoadReason(IReadOnlyList<string> via)
        {
            return $"No hay carretera desde vuestras zonas: contactad antes con {(via.Count > 0 ? JoinWith(via, "o") : "otra zona")}.";
        }

        /// <summary>The to-do line listing the zones under the Marea.</summary>
        public static string FloodedListLabel(int level, IReadOnlyList<string> zones)
        {
            return $"{(level >= PartituraLevel ? "Bajo la Marea" : "En manos de los muertos")}: {JoinWith(zones, "y")}";
        }

        /// <summary>A zone's card when it is under the Marea.</summary>
        public static string FloodedZoneNote(int level)
        {
            return level >= PartituraLevel
                ? "Bajo la Marea: de aquí no llega nada, y sus misiones traen un grupo más de muertos. Saldrá si la Marea retrocede: quemad Nidos."
                : "En manos de los muertos: de aquí no llega nada, y sus misiones traen un grupo más. Saldrá si baja la infestación: quemad Nidos.";
        }

        /// <summary>JavaScript's `list.slice(start, end)`.</summary>
        private static List<string> Slice(IReadOnlyList<string> list, int start, int end)
        {
            var result = new List<string>();
            for (int i = start; i < end && i < list.Count; i++) result.Add(list[i]);
            return result;
        }
    }
}
