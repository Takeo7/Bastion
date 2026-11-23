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

        /// <summary>
        /// Defence and dodge as the player sees them (M6, Arturo 23/11: «No me queda claro que aporta la esquiva exactamente ni
        /// defensa»; Gameplay's rule, gameplay/notas-23-11/M5-M7.md): defence takes its points off the attacker's odds
        /// (Aim.PreviewShot, as cover does), dodge turns its percent of what hits into grazes at half damage, crits first
        /// (ResolveShot). Both against whatever rolls (shots, overwatch, blows, bites, El Toro's fist), neither against
        /// explosions, gas, fire, falls or the scream. With the number, under the pointer, wherever the figure is: the
        /// soldier's sheet, the battle's card, an enemy's file, the board's totals (cuadro 116).
        /// </summary>
        public static class GuardText
        {
            /// <summary>«Defensa 10: quien le ataca acierta 10 puntos menos.»; with none, what still counts.</summary>
            public static string Defense(int n) => n > 0
                ? $"Defensa {n}: quien le ataca acierta {n} {(n == 1 ? "punto" : "puntos")} menos."
                : n < 0 ? $"Defensa {n}: quien le ataca acierta {-n} {(n == -1 ? "punto" : "puntos")} más."
                : "Defensa 0: sin defensa propia. Cuentan la cobertura y cubrirse.";
            /// <summary>«Esquiva 15: de lo que le acierten, el 15 % se queda en rozadura, con la mitad de daño.»</summary>
            public static string Dodge(int n) => n > 0
                ? $"Esquiva {n}: de lo que le acierten, el {n} % se queda en rozadura, con la mitad de daño."
                : "Esquiva 0: sin esquiva. Se gana con las barras de Forma física y en el tablero.";
            /// <summary>The line under either: what they count against.</summary>
            public const string Against = "Vale contra disparos, golpes y mordiscos. No contra explosiones, fuego ni caídas.";
        }
    }
}
