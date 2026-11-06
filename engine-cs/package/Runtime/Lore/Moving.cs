using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ moving

        /// <summary>
        /// The movement apart from the actions (docs/PRUEBA-2026-11-05-ARTURO.md #14): points a turn, a tile each and
        /// 1.5 on a diagonal, spent in stretches before and after acting; running is going past Content.QuietTiles of
        /// the turn's, in one stretch or several (Paths.IsRun), and the stretch that spends it all teaches Forma física
        /// (Paths.SpendsAll); whatever ends the turn spends what is left. The help's title and its rules, a line each,
        /// as NightText.
        /// </summary>
        public static class MoveText
        {
            public const string Label = "Moverse";
            public static readonly IReadOnlyList<string> Rules = new[]
            {
                $"Cada turno, un soldado tiene {Content.ApPerTurn} acciones ({Content.ApPerTurn + 1} desde Veterano) y {Content.SoldierMobility} puntos de movimiento. Cada casilla cuesta un punto; en diagonal, 1,5.",
                $"Moverse no gasta acciones, y el movimiento se reparte en tramos, antes y después de actuar: mover 2, disparar y mover {Content.QuietTiles - 2} más.",
                $"Correr es pasar de {Content.QuietTiles} puntos en el turno, en un tramo o en varios. El tramo que pasa de {Content.QuietTiles} se hace corriendo y se oye: mover 3, disparar y mover {Content.QuietTiles - 2} más es correr en el segundo.",
                "Forma física se llena con el tramo que gasta todo el movimiento, aunque antes haya habido otros.",
                "Lo que acaba el turno, como Vigilancia, Cubrirse, Esconderse o lanzar una granada, gasta también el movimiento que quede.",
                "Un soldado sin acciones pasa el mando aunque le quede movimiento: lo puede gastar más tarde.",
                "El turno acaba solo cuando nadie tiene acciones ni movimiento. Si no, pulsa «Terminar turno».",
                $"Un zombi tiene {Content.UndeadActions} acción y {Content.ZombieMobility} puntos de movimiento: un mordisco por turno.",
            };
        }
    }
}
