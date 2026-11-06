using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------- cover

        /// <summary>
        /// Cover hides from the dead too (docs/PRUEBA-2026-11-05-ARTURO.md #4): whatever stands between the zombie and
        /// the soldier, as for shots; behind low cover they see and notice at 3/4 of their range, behind high cover at
        /// half; hiding counts the most it hides, never both. The help's title and its rules, a line each, as NightText.
        /// </summary>
        public static class CoverText
        {
            public const string Label = "La cobertura y los zombis";
            public static readonly IReadOnlyList<string> Rules = new[]
            {
                "La cobertura también oculta de los zombis. Cuenta la que haya entre el zombi y el soldado, como en los disparos.",
                "Detrás de cobertura baja, un zombi ve y nota al soldado a tres cuartos de la distancia de siempre; detrás de cobertura alta, a la mitad.",
                "Esconderse no se suma a la cobertura: escondido, a la mitad, tenga cobertura o no.",
            };
        }
    }
}
