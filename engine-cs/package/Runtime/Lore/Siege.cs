using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------- siege

        /// <summary>The map edges, by DIRS4 index (Reinforcement.side: 0 north, 1 east, 2 south, 3 west).</summary>
        public static readonly IReadOnlyList<string> Sides = new[] { "norte", "este", "sur", "oeste" };

        /// <summary>
        /// The siege (docs/FASE12.md §10; playtest 18/10 #17, #20): the dead come at
        /// the base in waves and go for the living, from no side anyone can tell; the
        /// base falls with the last of its defenders. Headlines without a full stop,
        /// like the other missions' outcomes.
        /// </summary>
        public static class SiegeText
        {
            /// <summary>In place of MISSION_NAMES: the card and the strip.</summary>
            public const string Name = "Asedio";
            /// <summary>The skirmish menu's line under «El Bastión», like the other maps'.</summary>
            public const string Menu = "La muralla, cuatro torres y cinco oleadas";
            public const string Briefing = "Vienen en oleadas, cada una más grande, y van a por los vivos. Tiran puertas y barricadas para llegar a vosotros, y no se sabe por dónde entrarán.";
            /// <summary>Once the last wave is in.</summary>
            public const string NoMoreWaves = "No vienen más";
            /// <summary>The outcomes: held to the last turn, every wave down, nobody left.</summary>
            public const string Held = "El Bastión aguanta: los que quedan se retiran";
            public const string Cleared = "El Bastión aguanta: no queda ninguno en pie";
            public const string Wiped = "El Bastión ha caído: no queda nadie para defenderlo";
        }

        /// <summary>The strip's objective: a siege ends once every one of the dead is down (Ana, 23/10). `hold` is no longer used.</summary>
        public static string SiegeObjective(int? hold = null)
        {
            return "Acabad con todos";
        }

        /// <summary>The objective in full, for the card and the strip's tooltip.</summary>
        public static string SiegeObjectiveTip(int? hold = null)
        {
            return "Acabad con todas las oleadas. Si caen todos, el Bastión cae.";
        }

        /// <summary>Which turn a wave lands at the end of (`turn`: Reinforcement.turn), seen on squad turn `now`: «este turno», «próximo turno», «turno 5».</summary>
        private static string WaveTurn(int turn, int now)
        {
            if (turn <= now) return "este turno";
            return turn == now + 1 ? "próximo turno" : $"turno {turn}";
        }

        /// <summary>When the next wave lands, in full: «Próxima oleada: al final del turno 5.»</summary>
        public static string WaveDueLine(int turn, int now)
        {
            var when = WaveTurn(turn, now);
            return $"Próxima oleada: al final {(when == "este turno" ? "de" : "del")} {when}.";
        }

        /// <summary>The same, short for the strip: «Turno 5», «Próximo turno», «Este turno».</summary>
        public static string WaveDueShort(int turn, int now)
        {
            return Capital(WaveTurn(turn, now));
        }

        /// <summary>The log line as a wave comes in, from wherever it does: «Entra la oleada 2 de 5.»; the last one says so.</summary>
        public static string WaveEntersLine(int n, int total)
        {
            return n >= total ? "Entra la última oleada." : $"Entra la oleada {n} de {total}.";
        }

        /// <summary>A door or a barricade under the cursor: «Puerta: aguanta 12 de 18». `kind`: "door" or "barricade".</summary>
        public static string BreakableTip(string kind, int left, int full)
        {
            return $"{(kind == "door" ? "Puerta" : "Barricada")}: aguanta {left} de {full}";
        }

        /// <summary>The floating word when one gives way in sight, by kind ("door", "barricade").</summary>
        public static readonly IReadOnlyDictionary<string, string> GiveWayFloat = new Dictionary<string, string>
        {
            ["door"] = "CEDE LA PUERTA",
            ["barricade"] = "CEDE LA BARRICADA",
        };

        /// <summary>
        /// The log's line when one gives way, seen or not (`side`: a SIDES index):
        /// «Han tirado una puerta, al norte.», «Han abierto un hueco en la barricada
        /// del este.». A gate's leaves fall one by one, and a barricade's three tiles
        /// too: each says so.
        /// </summary>
        public static string GiveWayLine(string kind, int? side = null)
        {
            var where = side.HasValue && side.Value >= 0 && side.Value < Sides.Count ? Sides[side.Value] : null;
            if (kind == "door") return where != null ? $"Han tirado una puerta, al {where}." : "Han tirado una puerta.";
            return where != null ? $"Han abierto un hueco en la barricada del {where}." : "Han abierto un hueco en una barricada.";
        }

        /// <summary>The mark on the screen's edge and on the map where they broke in, like «Oleada · sur»: «Brecha · este».</summary>
        public static string BreachMark(int side)
        {
            return $"Brecha · {Sides[side]}";
        }
    }
}
