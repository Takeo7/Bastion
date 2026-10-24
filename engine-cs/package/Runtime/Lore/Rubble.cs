namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ rubble

        /// <summary>Walls and floors a grenade breaks (docs/FASE7.md §4–5): the log lines and the floating word.</summary>
        public static class RubbleText
        {
            /// <summary>Log, when the floor gives way under the blast.</summary>
            public const string Floor = "El suelo cede.";
            /// <summary>Floating over whoever falls through it.</summary>
            public const string FallFloat = "¡CAE!";
            /// <summary>Log, when the objective lying there goes down with the floor.</summary>
            public const string Item = "Lo que veníais a buscar cae con el suelo.";
        }

        /// <summary>The log line for a blast that breaks walls: one or several.</summary>
        public static string BreachLine(int walls)
        {
            return walls == 1 ? "La explosión abre un boquete en la pared." : "La explosión abre boquetes en las paredes.";
        }

        /// <summary>
        /// The log line for a fall through a broken floor: «Ana cae con el suelo a la
        /// planta de abajo.», «… dos plantas.». The damage (FALL_DAMAGE a storey) has
        /// its own line after, from `damaged`.
        /// </summary>
        public static string FallLine(string who, int storeys)
        {
            var n = System.Math.Max(1, storeys);
            var words = new[] { "dos", "tres" };
            return $"{who} cae con el suelo {(n == 1 ? "a la planta de abajo" : $"{(n - 2 < words.Length ? words[n - 2] : n.ToString())} plantas")}.";
        }
    }
}
