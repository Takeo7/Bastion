using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // --------------------------------------------------------------- scavenge

        /// <summary>Reports for the Saqueo mission (`scavenge`), in the style of BRIEFINGS.</summary>
        public static class ScavengeBriefings
        {
            public static readonly IReadOnlyList<string> All = new[]
            {
                $"Se vació a toda prisa, con las puertas cerradas y **las despensas llenas**. ==Registrad lo que podáis y volved {Bus.To}.== Cada tiro llama a más.",
                $"Nadie lo ha tocado **desde el primer invierno**. ==Llenad las mochilas y volved {Bus.To}.== Vendrán, y antes cuanto más ruido hagáis.",
                $"Cuando empezó todo, había gente dentro, y lo que dejaron sigue allí. ==Registrad, cargad y volved {Bus.To}== antes de que **la horda** os encuentre.",
            };
        }

        /// <summary>The Saqueo mission's lines for the combat screen and the debrief.</summary>
        public static class Scavenge
        {
            /// <summary>The mission's name (MISSION_NAMES).</summary>
            public const string Name = "Saqueo";
            /// <summary>Objective before anything is carried.</summary>
            public const string Objective = "Registrad lo que podáis y evacuad";
            /// <summary>Objective while the squad carries loot, short for the objective strip; `{n}` is how many things.</summary>
            public const string Carrying = "Botín: {n} · registrad o evacuad";
            /// <summary>The same, in full, for its tooltip.</summary>
            /// <remarks>Fixed in C# only: since #49 a fallen soldier's backpack stays on the ground, while lore.ts (frozen) still says it is lost.</remarks>
            public const string CarryingTip = "Lo que llevan las mochilas ({n}) cuenta si sale " + Bus.In + ". La mochila de un caído se queda en el suelo y se puede registrar. Lo que lleva quien se queda fuera, se pierde.";
            /// <summary>Debrief headline when loot came home.</summary>
            public const string Success = "Botín a salvo";
            /// <summary>Debrief headline when the squad came home with nothing.</summary>
            public const string EmptyHanded = "Volvéis con las mochilas vacías";
            /// <summary>The horde arrives at the edges.</summary>
            public const string Horde = "Llegan más zombis por los bordes";
        }
    }
}
