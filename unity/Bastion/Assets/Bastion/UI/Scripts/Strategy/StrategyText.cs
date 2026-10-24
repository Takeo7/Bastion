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

        /// <summary>
        /// marked.ts in rich text: **bold** and ==the goal== (in the accent), for briefings and the diary's paragraphs.
        /// </summary>
        public static string Marked(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
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
