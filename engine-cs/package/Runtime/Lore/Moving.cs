using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ moving

        /// <summary>
        /// The movement apart from the actions (docs/PRUEBA-2026-11-05-ARTURO.md #14): points a turn, a tile each and
        /// never a diagonal (Arturo, 7/11), spent in stretches before and after acting; running is all of it at once, in one stretch
        /// (Paths.IsRun; Arturo, 6/11), and the stretch that spends it all, at once or after others, teaches Forma física
        /// (Paths.SpendsAll); whatever ends the turn spends what is left. A soldier stays in play while it has anything left, or
        /// till its turn is ended (DoneCommand; Arturo, 7/11). The help's title and its rules, a line each,
        /// as NightText.
        /// </summary>
        public static class MoveText
        {
            public const string Label = "Moverse";
            public static readonly IReadOnlyList<string> Rules = new[]
            {
                $"Cada turno, un soldado tiene {Content.ApPerTurn} acciones ({Content.ApPerTurn + 1} desde Veterano) y {Content.SoldierMobility} puntos de movimiento. Cada casilla cuesta un punto, y nadie se mueve en diagonal.",
                $"Mientras la escuadra está oculta, cada soldado tiene {Content.ConcealedMobility} puntos más: {Content.SoldierMobility + Content.ConcealedMobility}. Al romperse la ocultación, pierde {Content.ConcealedMobility} de los que le queden ese turno.",
                $"Moverse no gasta acciones, y el movimiento se reparte en tramos, antes y después de actuar: mover 3, disparar y mover los {Content.SoldierMobility - 3} que quedan.",
                "Correr es gastar todo el movimiento de una vez, en un solo tramo, y se oye. Repartido en tramos, no se corre.",
                // N2 (Arturo, 22/11, cuadros 119 and 122).
                $"Bajar de una planta por una ventana, sin escalera, es saltar: quita {Content.FallDamage} de salud por planta, aunque lleve blindaje, y puede matar. No hace ruido.",
                "Si el soldado llega por la escalera con el movimiento que le queda, baja por ella. Si solo llega saltando, se avisa antes y hay que hacer clic otra vez. Cargando a alguien no se salta.",
                "Forma física se llena con el tramo que gasta todo el movimiento, de una vez o tras otros tramos.",
                "Solo Vigilancia (también la de cuerpo a cuerpo), Nadie pasa, Cubrirse y Esconderse acaban el turno, y gastan también el movimiento que quede.",
                "Un soldado sigue en juego mientras le queden acciones o movimiento. Para dejarlo antes, termina su turno: pierde lo que le quede.",
                "Entre dos jugadores, se juega un soldado cada vez: el mando pasa al otro cuando el tuyo ya no tiene nada o terminas su turno.",
                "El turno acaba solo cuando nadie tiene acciones ni movimiento. Si no, pulsa «Terminar turno».",
                $"Un zombi tiene {Content.UndeadActions} acción y {Content.ZombieMobility} puntos de movimiento: un mordisco por turno.",
            };
        }

        /// <summary>
        /// N2 (Arturo, 22/11, cuadros 119 and 122): a jump down off a storey without a ladder costs Content.FallDamage of health a
        /// storey, through armour, and may kill. The way's note under the destination, as the fire's («Fuego en el camino: −2 de
        /// salud»; Paths.FallAlong), and the first click's warning: the jump goes on the second.
        /// </summary>
        public static class JumpText
        {
            /// <summary>«Salto de una planta: −1 de salud», «Salto de 3 plantas: −3 de salud, y muere» (a «,», not a second «:»).</summary>
            public static string Note(int storeys, bool dies)
            {
                var n = System.Math.Max(1, storeys);
                var line = $"Salto de {(n == 1 ? "una planta" : $"{n} plantas")}: −{n * Content.FallDamage} de salud";
                return dies ? $"{line}, y muere" : line;
            }

            /// <summary>The first click's: «Salto de 2 plantas: −2 de salud. Haz clic otra vez para saltar.»</summary>
            public static string Ask(int storeys, bool dies, bool touch) => $"{Note(storeys, dies)}. {(touch ? "Toca" : "Haz clic")} otra vez para saltar.";

            /// <summary>Carrying someone there is no jumping (cuadro 122): why a tile reached only by a jump is out of reach.</summary>
            public const string Carrying = "Cargando a alguien no se salta: hay que bajar por la escalera.";
        }

        /// <summary>
        /// The storey shown and how to change it (N4, cuadro 121): the fixed selector («▲ Planta 1 ▼») and, aiming something
        /// thrown, the line with the storey and its keys (the help's «RePág / AvPág»). «Planta», as the game says it everywhere
        /// («Subir de planta», RubbleText.FallLine), not «piso».
        /// </summary>
        public static class FloorText
        {
            public static string Name(int z) => z <= 0 ? "Planta baja" : $"Planta {z}";
            /// <summary>The keys that change it: on the Mac, whose keyboard has no RePág nor AvPág, Fn with the arrows (UI, 23/11).</summary>
            public static string Keys(bool mac) => mac ? "Fn + ↑ / Fn + ↓" : "RePág / AvPág";
            /// <summary>«Planta 1 · RePág / AvPág: cambiar de planta»; on the Mac, «Fn + ↑ / Fn + ↓»; with the finger, the selector's arrows.</summary>
            public static string Aim(int z, bool touch, bool mac = false) => $"{Name(z)} · {(touch ? "▲ ▼" : Keys(mac))}: cambiar de planta";
        }
    }
}
