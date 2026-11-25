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

        /// <summary>The skill board: its points' help.</summary>
        public static class Board
        {
            /// <summary>The health in a run with its vest (Producto, 24/11), over the totals' line (Lore's yes, 24/11).</summary>
            public static string HpWithVest(int hp, int bare, int vest, string vestName) => $"Salud {hp}: {bare} + {vest} del {vestName.ToLowerInvariant()}";
            public const string PointsTip = "Un punto de habilidad cada vez que se llena una barra";
            /// <summary>N6 (22/11; till Lore's words): over what a cell changes, and over the soldier's totals.</summary>
            public const string Gains = "Si la coges";
            public const string Totals = "Totales";
            /// <summary>The totals' words, by key (StrategyPresenter.BoardTotals).</summary>
            public static readonly Dictionary<string, string> Stats = new Dictionary<string, string>
            {
                ["hp"] = "Salud", ["armor"] = "Blindaje", ["ap"] = "Acciones", ["moves"] = "Movimiento",
                ["gunAim"] = "Puntería al disparar", ["meleeAim"] = "Puntería cuerpo a cuerpo", ["gunCrit"] = "Crítico al disparar",
                ["meleeCrit"] = "Crítico cuerpo a cuerpo", ["dodge"] = "Esquiva", ["defense"] = "Defensa", ["will"] = "Voluntad",
                ["sight"] = "Vista", ["heal"] = "Cura extra",
            };

            /// <summary>The totals shown with their sign, «Cura extra +2» (Lore, 22/11: «de más» read as «too much»).</summary>
            public static readonly HashSet<string> Signed = new HashSet<string> { "heal" };
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
        /// <summary>A game of expeditions lost (N8): another one from here, as hard as this one (Lore's «empezad una partida nueva»).</summary>
        public static class Lost
        {
            public const string Again = "Nueva partida";
            public const string Start = "Empezar";
            public const string Back = "Volver";
        }

        public static class Leave
        {
            public const string Kicker = "Menú principal";
            public const string Title = "¿Volver al menú principal?";
            public const string Yes = "Volver al menú";
            public const string No = "Seguir aquí";

            public static string Text(string partner) => string.IsNullOrEmpty(partner) ? "La campaña queda guardada." : $"La campaña queda guardada. {partner} vuelve también al menú.";
        }

        /// <summary>The runs' base (16/11; Lore's names: «Expedición», «Etapa», «Capítulo»).</summary>
        /// <summary>The soldier sheet's three lines, each its word (Producto's review, 23/11, B9): the battle HUD's own.</summary>
        public static class Sheet
        {
            public const string Health = "Salud";
            public const string Actions = "Acciones";
            public const string Moves = "Movimiento";
        }

        public static class Runs
        {
            public static string Chapter(int n) => $"CAPÍTULO {n}";
            public const string ChapterTip = "El capítulo que toca. Se supera matando a su jefe.";

            // The map: Lore's own (Lore.ExpeditionText, StageName, StageLine, ModifierName, ModifierLine in the engine).
            public static string Head(int chapter) => Lore.ExpeditionText.Header(chapter);
            public const string Choose = "Elige la siguiente etapa.";
            public const string NoWay = "No queda más camino.";
            /// <summary>
            /// «Tus soldados llevan [95 and the crate].»: the words round the amount; each player pays with their own
            /// soldiers' (Lore, 16/11: «lleváis» sounded like the whole squad's).
            /// </summary>
            public const string PurseBefore = "Tus soldados llevan";
            public const string PurseAfter = ".";

            public static string For(string name) => $"Lo que cambies va a la mochila de {name}.";
            // M10 (cuadro 141), the Trueque's two sides (UI's, Lore's yes, 23/11). The § go where Gears.Sell puts them.
            public const string Wares = "Lo que te ofrece";
            public const string Offers = "Lo que le ofreces";
            public static string OffersFrom(string name) => $"Lo que lleva {name}, puesto y en la mochila. Los § van a su mochila; si no caben, a la de otro de tus soldados.";
            public const string Worn = "Lo lleva puesto";
            /// <summary>
            /// Worn, agreeing with the thing (Lore, 24/11: «2 botiquines · Lo lleva puesto» didn't): «Lo lleva puesto», «La lleva
            /// puesta», «Los lleva puestos», «Las lleva puestas».
            /// </summary>
            public static string WornOf(string kind, string type, int count)
            {
                var (female, many) = Agreement(kind, type, count);
                return $"{(female ? "La" : "Lo")}{(many ? "s" : "")} lleva puest{(female ? "a" : "o")}{(many ? "s" : "")}";
            }
            /// <summary>Whether a thing is said in the feminine and in the plural, by its article in Lore.GearThe.</summary>
            static (bool female, bool many) Agreement(string kind, string type, int count)
            {
                var one = Lore.GearThe(kind, type, 1);
                return (one.StartsWith("la ") || one.StartsWith("las "), count > 1 || one.StartsWith("los ") || one.StartsWith("las "));
            }
            public static string NothingToOffer(string name) => $"{name} no lleva nada que se pueda cambiar.";
            /// <summary>Under it with more than one soldier (Producto, 24/11): the busts on the right choose whose (Lore's yes, 24/11).</summary>
            public const string OffersOther = "Para ofrecer lo de otro soldado, elige su busto a la derecha.";
            /// <summary>
            /// The question before selling what is worn (Producto, 24/11: it goes for good, at one click), in the row itself:
            /// «¿Cambiar el fusil de cerrojo que lleva puesto por 55 §?», «puesta» for «la pistola» (Lore's yes, 24/11).
            /// </summary>
            public static string SellWorn(string kind, string type, int count, string price)
            {
                var one = Lore.GearThe(kind, type, 1);
                var (female, many) = Agreement(kind, type, count);
                // More than one comes without its article («2 granadas»): «las 2 granadas que lleva puestas» (Lore, 24/11).
                var the = count > 1 ? $"{(female ? "las" : "los")} {Lore.GearThe(kind, type, count)}" : one;
                return $"¿Cambiar {the} que lleva puest{(female ? "a" : "o")}{(many ? "s" : "")} por {price.TrimStart('+')} §?";
            }
            /// <summary>Leaves the question: «Cancelar», as Partir and Tirar (Lore, 16/11 and 24/11).</summary>
            public const string SellNo = "Cancelar";
            public static string Waiting(string partner) => $"Esperando a {partner}.";
            public static string TheyWait(string partner) => $"{partner} quiere seguir.";

            // Mejoras (Lore, NOMBRES.md §5: «Mejorar», never «comprar»; «Se gastan 120 § del almacén.»).
            public const string UpgradesHead = "Mejoras";
            public const string Upgrade = "Mejorar";
            public const string UpgradeTop = "Al máximo";
            /// <summary>An upgrade's level in words beside its marks (Producto's review, 23/11, B12; Lore's yes).</summary>
            public static string UpgradeLevel(int level, int levels) => level <= 0 ? "Sin mejorar" : level >= levels ? $"Nivel {level} de {levels}: al máximo" : $"Nivel {level} de {levels}";
            /// <summary>Under the Trueque's purse (B18): only what the soldiers carry counts out there (Lore, 23/11).</summary>
            public const string PurseOnly = "En una expedición solo cuenta lo que llevan encima: lo del almacén se queda en la base.";
            public static string UpgradeTip(int price) => $"Se gastan {price} § del almacén.";

            public static string UpgradeName(string id) => Lore.UpgradeNames.TryGetValue(id, out var n) ? n : id;

            /// <summary>What the next level does; `seats`, how many go out now, `next` after it (Más plazas).</summary>
            public static string UpgradeLine(string id, int seats, int next) => Lore.UpgradeLine(id, seats, next, Bastion.Engine.Runs.MendHeal);

            // Salir (Lore: «Salir», its help «Sale la escuadra: empieza una expedición del capítulo {n}.»).
            public const string Start2 = Lore.ExpeditionText.Start;
            public static string StartTip(int chapter) => Lore.ExpeditionText.StartHelp(chapter);
            /// <summary>La Lata as the seats make it (N4, 17/11; Lore.RunTransport): «La Lata es **una moto**: 2 plazas.»</summary>
            public static string Transport(int seats) => $"{Lore.Bus.Name} es **{Lore.RunTransport(seats).A}**: {seats} plazas.";
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
            /// <summary>A stage's report with nothing found nor a reward (the pass of 16/11: only its title was left; Lore to confirm).</summary>
            public const string NothingFound = "No habéis encontrado nada.";

            /// <summary>«Rafael ha caído. Se pierden con él el fusil y 2 granadas.»</summary>
            /// <summary>A Rescate won (H4): who was held out joins (Lore to confirm).</summary>
            public static string JoinedLine(string name) => $"<b>{name}</b> se une a la escuadra.";

            /// <summary>
            /// A survivor with no bed in the Bastión (RunReport.NoBed; Producto's review of 24/11): they don't stay; what they carried
            /// (all but their knife) goes to the squad's packs, and what didn't fit stays there. `gave`: anything went to the packs.
            /// </summary>
            public static string NoBedLine(string name, bool gave, IList<string> lost) =>
                $"<b>{name}</b> no se queda: en el Bastión ya no caben más soldados."
                + (lost.Count == 0 ? (gave ? " Deja a la escuadra lo que llevaba." : "")
                    : gave ? $" Deja a la escuadra lo que cabía en las mochilas; lo demás se queda allí: {Ui.List(lost)}."
                    : $" Lo que llevaba no cabía en las mochilas y se queda allí: {Ui.List(lost)}.");
            public static string FallenLine(string name, IList<string> lost) => lost.Count == 0
                ? $"<b>{name}</b> ha caído."
                : $"<b>{name}</b> ha caído. Se pierde lo que llevaba: {Ui.List(lost)}.";

            /// <summary>
            /// One left outside La Lata as it went (Red's pass, 18/11: not «ha caído»), «la» or «lo» by who it is: «Yuki se
            /// quedó fuera de La Lata. La perdéis con lo que llevaba: el cuchillo.»
            /// </summary>
            public static string LeftLine(string name, bool female, IList<string> lost) => lost.Count == 0
                ? $"<b>{name}</b> se quedó fuera de {Lore.Bus.Name}. {(female ? "La" : "Lo")} perdéis."
                : $"<b>{name}</b> se quedó fuera de {Lore.Bus.Name}. {(female ? "La" : "Lo")} perdéis con lo que llevaba: {Ui.List(lost)}.";

            /// <summary>Asked before going back (alone it is at once).</summary>
            public const string EndAsk = "¿Volver a la base?";
            public const string EndNo = "Seguir la expedición";
            public const string Squad = "Escuadra";
            public const string Here = "AQUÍ";
            /// <summary>The map on paper (cuadro 132): the stage card's words, the partner's mark, the legend, the squad folded.</summary>
            public const string Go = "Ir a esta etapa";
            public const string WhatHead = "Qué hay que hacer";
            public const string WhereHead = "Dónde";
            /// <summary>«Etapa 4 de 7», as the room says it (Lore, 23/11: «fila» is a word of the inside); the boss's, «7 de 7».</summary>
            public static string Kicker(int row) => $"Etapa {row} de {Bastion.Engine.RunMap.Rows}";
            public static string Danger(int d) => $"Peligro {d}";
            public static string ProposedBy(string name) => $"Propuesta de {name}";
            public const string ProposedMine = "Tu propuesta";
            public const string KitOpen = "Equipo ▸";
            public const string KitClose = "Mapa ◂";
            public const string ChapterEnd = "Fin del capítulo";
            /// <summary>The card with none chosen, and on one under the pointer that isn't (cuadro 135; Lore's words, 23/11).</summary>
            public const string PickHead = "Elige una etapa";
            public static string PickHint(bool touch) => touch ? "Toca una etapa rodeada en rojo para ver qué es y elegirla." : "Haz clic en una etapa rodeada en rojo para elegirla. Pasa el puntero por cualquiera para ver qué es.";
            public static string ToPick(bool touch) => touch ? "Tócala para elegirla." : "Haz clic en ella para elegirla.";
            public const string LegendHead = "Leyenda";
            /// <summary>The legend: the modifiers by their name alone (Lore: «más botín» told half; the card says it whole), then the ways.</summary>
            public static readonly string[] Legend = { "De noche", "Mal tiempo", "Horda despierta", "Se puede ir", "Recorrido", "Más adelante" };
            /// <summary>The Trueque's button (Lore: «cambiar», never «comprar»); «Seguir» from a stop.</summary>
            public const string Swap = "Cambiar";
            public const string Leave = "Seguir";
            public const string Unseen = "Aún no se ve qué hay.";
            /// <summary>After a modifier an event left on a stage (Lore to confirm).</summary>
            public const string ByEvent = " (por un suceso del camino)";
            public static string Site(string place) => $"Sitio: {place}.";
            /// <summary>A stage's danger (cuadro 115: «Peligro»; «Dificultad» is only the game's level).</summary>
            public static string Difficulty(int d) => $"Peligro {d}.";

            /// <summary>
            /// A stage's name and line (Lore's); a kind Lore has no words for yet, «Etapa» and none, not the whole map gone white
            /// (the review of 23/11: Las Cargas threw there and the screen was empty). Said in the log, to give it words.
            /// </summary>
            public static string NodeName(string kind, int chapter = 1) => Safe(() => Lore.StageName(kind, chapter), kind, Unnamed);
            public static string NodeLine(string kind) => Safe(() => Lore.StageLine(kind), kind, "");
            public const string Unnamed = "Etapa";

            /// <summary>The stage's line with what is known of it (Runs.StageLine), or none for a kind without words.</summary>
            public static string StageLine(RunNode n) => Safe(() => Bastion.Engine.Runs.StageLine(n), n.Kind, "");

            static string Safe(System.Func<string> words, string kind, string none)
            {
                try
                {
                    return words();
                }
                catch (System.Exception e) when (e is System.ArgumentException || e is KeyNotFoundException)
                {
                    UnityEngine.Debug.LogWarning($"[runs] el tipo de etapa «{kind}» no tiene textos de Lore: sale «{Unnamed}».");
                    return none;
                }
            }
            /// <summary>The weather isn't known until the stage: «Mal tiempo» (Lore).</summary>
            public static string ModifierName(string id) => Lore.ModifierName(id);
            public static string ModifierLine(string id) => Lore.ModifierLine(id);
        }

        /// <summary>Almacén, the runs' base (16/11; Lore to confirm). The engine's reasons come as they are.</summary>
        public static class Stash
        {
            public const string Head = "Almacén";
            /// <summary>Right over a weapon's figures weighed against a soldier's (T4; Lore's words): «Frente al fusil de cerrojo de Anja (verde, mejor; rojo, peor):».</summary>
            public static string Against(string the, string who) => $"Frente {(the.StartsWith("el ") ? "al " + the.Substring(3) : "a " + the)} de {who} (verde, mejor; rojo, peor):";
            /// <summary>A soldier's bonus on a weapon whose source has no name (O7: a fight's mods the sources don't explain).</summary>
            public const string Others = "otros";
            /// <summary>Under a gun's whole aim (cuadro 156; Lore's words).</summary>
            public const string NoReachNorCover = "sin contar distancia ni cobertura";
            /// <summary>
            /// Under a figure the soldier changes (O7, Producto's words): the soldier's own base if it has a part (aim and
            /// crit, cuadro 156), the weapon's, then each of the soldier's that touches it, by its name: «arma 3–5 · Golpe
            /// pesado +1», «base 55 · arma +15».
            /// </summary>
            public static string FromWeapon(string weapon, IEnumerable<string> bonuses, string own = null)
            {
                var parts = new List<string>();
                // The soldier's own first, when it has a part (aim and crit: «base 55 · arma +15»).
                if (own != null) parts.Add($"base {own}");
                parts.Add($"arma {weapon}");
                parts.AddRange(bonuses);
                return string.Join(" · ", parts);
            }
            /// <summary>
            /// A weapon's reach in tiles (T23, Lore's words): how far first, then where it's best, so no figure has to be guessed:
            /// «hasta 18 casillas; mejor de 1 a 11», «hasta 18 casillas; mejor desde 11», «hasta 18 casillas»; a blade «1 casilla».
            /// </summary>
            public static string Reach(int from, int to, int max, bool melee = false)
            {
                string Tiles(int n) => $"{n} {(n == 1 ? "casilla" : "casillas")}";
                // (A blade's, the rake's two too: as far as it reaches, no «hasta».)
                if (melee || (from == to && to == max)) return Tiles(max);
                if (from <= 1 && to >= max) return $"hasta {Tiles(max)}";
                return to >= max ? $"hasta {Tiles(max)}; mejor desde {from}" : $"hasta {Tiles(max)}; mejor de {from} a {to}";
            }
            /// <summary>An empty place of a kit, round the figure in Soldados (T3).</summary>
            public const string Nothing = "Nada";
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
