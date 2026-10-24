using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ story

        /// <summary>The chapter whose mission needs Simão's visit answered first.</summary>
        public const int SimonChapter = 5;
        /// <summary>From this chapter on Dr Nwosu is in the Bastion: the loot is worth more.</summary>
        public const int NwosuChapter = 2;

        // ---------------------------------------------------------------- endings

        /// <summary>How the two commanders ended it, with both keys (Ending: "break", "record"): the vaccine for everyone, or for the Red.</summary>
        public static readonly IReadOnlyDictionary<string, string> EndingTitles = new Dictionary<string, string>
        {
            ["break"] = "La vacuna, para todos",
            ["record"] = "La vacuna, para quienes cumplieron",
        };

        /// <summary>The last page of the campaign. `outcome`: "victory" or "defeat"; `ending`: "break", "record" or null.</summary>
        public static string Epilogue(string outcome, string? ending, bool doomFull)
        {
            if (outcome == "victory")
            {
                if (ending == "break")
                {
                    return "La Marea se deshizo en la sierra. Esa noche, Morse leyó la fórmula de la vacuna por todas las frecuencias, despacio, dos veces. La oyeron los enclaves, la oyeron los Cuervos y la oyó quien quisiera oírla. Nadie sabe qué harán con ella. Por primera vez en dos inviernos, nadie se queda fuera.";
                }
                if (ending == "record")
                {
                    return "La Marea se deshizo en la sierra. La vacuna salió del Bastión en La Lata, punto de cambio por punto de cambio, solo para quienes cumplieron sus tratos. El valle sobrevive. Fuera de él, la radio sigue pidiendo ayuda. La fórmula se queda en el Bastión, bajo llave.";
                }
                return "La Marea se ha deshecho en la sierra. Por primera vez en dos inviernos, el valle duerme sin oír pasos.";
            }
            return doomFull
                ? "Kilómetro cero. La Marea llegó al Bastión con la primera niebla. Morse siguió emitiendo hasta el final. La última hoja del diario se quedó a medio escribir."
                : "No queda nadie que conteste por radio. El Bastión se queda a oscuras.";
        }
    }
}
