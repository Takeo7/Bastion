using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bastion.Engine;

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
        public static string MenuTip => Bastion.UI.Hud.HudText.Keyed("Menú", "Esc");

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

        /// <summary>«Volver al menú principal» from the base (Lore, 24/10: as in the HUD; «menú principal», not «de inicio»).</summary>
        public static class Leave
        {
            public const string Kicker = "Menú principal";
            public const string Title = "¿Volver al menú principal?";
            public const string Yes = "Volver al menú";
            public const string No = "Seguir aquí";

            public static string Text(string partner) => string.IsNullOrEmpty(partner) ? "La campaña queda guardada." : $"La campaña queda guardada. {partner} vuelve también al menú.";
        }

        /// <summary>The runs' base (16/11; Lore's names: «Expedición», «Etapa», «Capítulo»).</summary>
        public static class Runs
        {
            public static string Chapter(int n) => $"CAPÍTULO {n}";
            public const string ChapterTip = "El capítulo que toca. Se supera matando a su jefe.";

            // The map: Lore's own (Lore.ExpeditionText, StageName, StageLine, ModifierName, ModifierLine in the engine).
            public static string Head(int chapter) => Lore.ExpeditionText.Header(chapter);
            public const string Choose = "Elige la siguiente etapa.";
            public const string NoWay = "No queda más camino.";
            /// <summary>«Lleváis [95 and the crate] en las mochilas.»: the words round the amount.</summary>
            public const string PurseBefore = "Lleváis";
            public const string PurseAfter = "en las mochilas.";

            public static string For(string name) => $"Lo que cambiéis va a la mochila de {name}.";
            public const string Join = "Que se una";
            public const string NoJoin = "Que no";
            public static string Waiting(string partner) => $"Esperando a {partner}.";
            public static string TheyWait(string partner) => $"{partner} quiere seguir.";

            // Mejoras (Lore, NOMBRES.md §5: «Mejorar», never «comprar»; «Se gastan 120 § del almacén.»).
            public const string UpgradesHead = "Mejoras";
            public const string Upgrade = "Mejorar";
            public const string UpgradeTop = "Al máximo";
            public static string UpgradeTip(int price) => $"Se gastan {price} § del almacén.";

            public static string UpgradeName(string id) => Lore.UpgradeNames.TryGetValue(id, out var n) ? n : id;

            /// <summary>What the next level does; `seats`, how many go out now, `next` after it (Más plazas).</summary>
            public static string UpgradeLine(string id, int seats, int next) => Lore.UpgradeLine(id, seats, next, Bastion.Engine.Runs.MendHeal);

            // Salir (Lore: «Salir», its help «Sale la escuadra: empieza una expedición del capítulo {n}.»).
            public const string Start2 = Lore.ExpeditionText.Start;
            public static string StartTip(int chapter) => Lore.ExpeditionText.StartHelp(chapter);
            public const string BackToStash = "Volver al Almacén";
            public const string SquadTip = "Pulsa un soldado para meterlo o sacarlo. El equipo se cambia en el Almacén.";
            public static string SquadTipTwo(string partner) => $"Pulsa un soldado para meterlo o sacarlo. Los de {partner} los elige {partner}. El equipo se cambia en el Almacén.";
            public const string Start = "La base";
            public const string Back = Lore.ExpeditionText.Return;
            public const string BackTip = Lore.ExpeditionText.ReturnHelp;
            /// <summary>The report's head after a stage (H3; Lore to confirm): how it ended.</summary>
            public static string Ended(string ended, bool won, string kind)
            {
                switch (ended)
                {
                    case "wiped": return "Escuadra perdida";
                    case "boss": return "Capítulo superado";
                    case "returned": return "De vuelta a la base";
                }
                var objective = kind != "normal" && kind != null;
                return won || !objective ? "Etapa superada" : "Etapa superada, sin la recompensa";
            }

            /// <summary>The reward's tile, on hover; what of it didn't fit in the packs (no «§» in the line: the crate is on the tile).</summary>
            public static string RewardTip(int reward) => $"{Lore.ExpeditionText.Reward}: {reward} suministros.";
            public static string RewardLost(int lost) => $"De la recompensa, {lost} suministros no cabían en las mochilas y se han quedado allí.";

            /// <summary>«Rafael ha caído. Se pierden con él el fusil y 2 granadas.»</summary>
            /// <summary>A Rescate won (H4): who was held out joins (Lore to confirm).</summary>
            public static string JoinedLine(string name) => $"<b>{name}</b> se une a la escuadra.";

            public static string FallenLine(string name, IList<string> lost) => lost.Count == 0
                ? $"<b>{name}</b> ha caído."
                : $"<b>{name}</b> ha caído. Se pierde lo que llevaba: {Ui.List(lost)}.";

            /// <summary>Asked before going back (alone it is at once).</summary>
            public const string EndAsk = "¿Volver a la base?";
            public const string EndNo = "Seguir la expedición";
            public const string Squad = "Escuadra";
            public const string Here = "AQUÍ";
            /// <summary>The Trueque's button (Lore: «cambiar», never «comprar»); «Seguir» from a stop.</summary>
            public const string Swap = "Cambiar";
            public const string Leave = "Seguir";
            public const string Unseen = "Aún no se ve qué hay.";
            /// <summary>After a modifier an event left on a stage (Lore to confirm).</summary>
            public const string ByEvent = " (por un suceso del camino)";
            public static string Site(string place) => $"Sitio: {place}.";
            public static string Difficulty(int d) => $"Dificultad {d}.";

            public static string NodeName(string kind, int chapter = 1) => Lore.StageName(kind, chapter);
            public static string NodeLine(string kind) => Lore.StageLine(kind);
            /// <summary>The weather isn't known until the stage: «Mal tiempo» (Lore).</summary>
            public static string ModifierName(string id) => Lore.ModifierName(id);
            public static string ModifierLine(string id) => Lore.ModifierLine(id);
        }

        /// <summary>Almacén, the runs' base (16/11; Lore to confirm). The engine's reasons come as they are.</summary>
        public static class Stash
        {
            public const string Head = "Almacén";
            public const string SuppliesTip = "Suministros del almacén. Con ellos se mejora la base.";
            public static string Whose(string player) => $"De {player}";
            public static string Theirs(string player) => $"Es de {player}: su equipo solo lo cambia {player}.";
            /// <summary>«salud», the game's word (Lore, NOMBRES.md §5), not «vida».</summary>
            public static string Health(int hp, int max) => $"{hp} de {max} de salud";

            public const string Mags = "Cargadores";
            public const string Medicine = "Medicinas";
            public const string Supplies = "Suministros";
            public const string Primary = "Arma principal.";
            public const string Sidearm = "Arma corta.";
            public const string Melee = "Arma de cuerpo a cuerpo.";
            public static string Mounted(IList<string> mods) => $"Lleva {Ui.List(mods)}.";
            public static string Vest(int hp, int armor) => armor > 0 ? $"+{hp} de salud y blindaje {armor}: resta {armor} de daño a cada golpe." : $"+{hp} de salud.";
            public const string MagsLine = "Recargar gasta uno. Valen para todas las armas.";
            public const string MedicineLine = "Cura 3 de salud, en combate o entre etapas.";
            public const string SuppliesLine = "Sirven para mejorar la base y para cambiar en el Trueque.";

            /// <summary>«Quitar la mira» (Lore: with its article, GearThe).</summary>
            public static string Unmount(string theMod) => $"Quitar {theMod}";
            public static string Heal(string name) => $"Curar a {name}";
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
            return Regex.Replace(text, @"==(.+?)==", "<color=#ffffff><b>$1</b></color>");
        }

        /// <summary>A tooltip's sentence with its figures picked out (screen.ts figures: em.tip-num).</summary>
        public static string Figures(string sentence)
        {
            if (string.IsNullOrEmpty(sentence)) return sentence;
            return Regex.Replace(sentence, @"([−+-]?\d+(?:[.,]\d+)?\s?(?:%|§|✚)?)", "<color=#ffffff><b>$1</b></color>");
        }
    }
}
