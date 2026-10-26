using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base's fixed words that aren't the campaign's (screen.ts's own), and the small formatting the 0.10.1 did in
    /// HTML: **bold** and ==the goal== (marked.ts), the figures in a tooltip (figures). Lore reviews the words.
    /// </summary>
    public static class StrategyText
    {
        public const string Brand = "BASTIÓN";
        /// <summary>The stamp as La Lata leaves (screen.ts launch).</summary>
        public const string Launch = "En marcha";
        public const string Menu = "Menú";
        public const string MenuTip = "Menú (Esc)";

        public static class Proposal
        {
            public const string Kicker = "Propuesta";
            public const string MineKicker = "Tu propuesta";
            public const string See = "Ver";
            public const string Accept = "Aceptar";
            public const string Reject = "Rechazar";
            public const string Withdraw = "Retirar";

            /// <summary>«Ana propone reclutar un soldado.», her name in her colour.</summary>
            public static string Theirs(string name, string color, string text) => $"<color={color}><b>{name}</b></color> propone {text}.";

            public static string Mine(string text, string partner) => $"Has propuesto {text}. Esperando a {partner}…";
            public static string CostTheirs(string cost) => $"Si aceptas, se paga: {cost}.";
            public static string CostMine(string cost) => $"Se paga al aceptar: {cost}.";
        }

        public static class Tabs
        {
            public const string NewTag = "Nuevo";
            public const string NewTip = "Hay cosas nuevas";
            public static string PartnerHere(string name) => $"{name} está aquí";
        }

        /// <summary>The soldier's sheet: the heads' and the veterancy's help (screen.ts renderSheet's titles).</summary>
        public static class Sheet
        {
            public const string ArmsTip = "Las armas salen de la Armería: allí se arreglan y se les ponen mejoras";
            public const string BeltTip = "Dos huecos para cualquier accesorio. Se compran en la Armería; si el soldado cae, se pierden";
            public const string RankTip = "La veteranía sube con los puntos gastados en el tablero: un rango cada 5.";
        }

        /// <summary>The skill board: its points' help.</summary>
        public static class Board
        {
            public const string PointsTip = "Un punto de habilidad cada vez que se llena una barra";
        }

        /// <summary>Escuadra's and the line-up's fixed words.</summary>
        public static class Squad
        {
            public const string RemoveTip = "Quitar de la escuadra";
            public const string InTip = "En la escuadra";
        }

        /// <summary>A card's «→ +2 Habilidades» under the line-up (skillSheet.ts:182): the points for the board a mission gave.</summary>
        public static class Learned
        {
            public static string Points(int n) => n == 1 ? "Habilidad" : "Habilidades";
            public const string PointsTip = "Puntos para el tablero de habilidades";
        }

        /// <summary>The Mapa's fixed help.</summary>
        public static class Map
        {
            public const string NetworkTip = "Pulsa una zona del mapa para ver sus datos";
            public const string ZoneMeterTip = "Zonas contactadas / máximo";
            /// <summary>The zone's sheet: the head over its missions.</summary>
            public const string ZoneMissions = "Misiones en la zona";
        }

        /// <summary>The radio's card (transmissions.ts): its fixed words.</summary>
        public static class Radio
        {
            public const string Kicker = "Transmisión";
            public const string CloseTip = "Cerrar (Esc)";
            public const string Next = "Seguir";
            public const string End = "Fin";
            public const string Ok = "Entendido";
            /// <summary>After «Seguir ▸» once the line is typed (the battle's card takes clicks only: Hint without «o Intro»).</summary>
            public const string Hint = "· clic o Intro";

            /// <summary>{saludo} and {yo}: «Comandantes Ana y Luis», «Comandante Ana» (transmissions.ts greeting).</summary>
            public static string Greeting(IList<string> names)
            {
                var list = new List<string>();
                foreach (var n in names) if (!string.IsNullOrEmpty(n)) list.Add(n);
                if (list.Count == 0) return "Comandantes";
                if (list.Count == 1) return $"Comandante {list[0]}";
                return $"Comandantes {string.Join(", ", list.GetRange(0, list.Count - 1))} y {list[list.Count - 1]}";
            }
        }

        /// <summary>«Volver al menú principal» from the base (Lore, 24/10: as in the HUD; «menú principal», not «de inicio»).</summary>
        public static class Leave
        {
            public const string Kicker = "Menú principal";
            public const string Title = "¿Volver al menú principal?";
            public const string Yes = "Volver al menú";
            public const string No = "Seguir aquí";

            public static string Text(string partner) => string.IsNullOrEmpty(partner) ? "La campaña queda guardada." : $"La campaña queda guardada. {partner} vuelve también al menú.";
        }

        /// <summary>A text a sentence a line, as the 0.10.1's tooltips (screen.ts sentences: «no un bloque», Ana 21/10 #33).</summary>
        public static List<string> Sentences(string text)
        {
            var out_ = new List<string>();
            if (string.IsNullOrEmpty(text)) return out_;
            foreach (var part in Regex.Split(text.Trim(), @"(?<=[.!?])\s+(?=[A-ZÁÉÍÓÚÑ¡¿−+0-9«])"))
                if (part.Length > 0) out_.Add(part);
            return out_;
        }

        /// <summary>
        /// marked.ts in rich text: **the key words** in white bold and ==the goal== in the accent (mixta-base.css strong,
        /// mark.goal), for briefings, the arrival card and the diary's paragraphs.
        /// </summary>
        public static string Marked(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<color=#ffffff><b>$1</b></color>");
            return Regex.Replace(text, @"==(.+?)==", "<color=#f2c230><b>$1</b></color>");
        }

        /// <summary>A tooltip's sentence with its figures picked out (screen.ts figures: em.tip-num).</summary>
        public static string Figures(string sentence)
        {
            if (string.IsNullOrEmpty(sentence)) return sentence;
            return Regex.Replace(sentence, @"([−+-]?\d+(?:[.,]\d+)?\s?(?:%|§|✚)?)", "<color=#f2c230><b>$1</b></color>");
        }
    }
}
