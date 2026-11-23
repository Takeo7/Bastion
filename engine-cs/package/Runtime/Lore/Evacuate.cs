using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ---------------------------------------------------------------- evacuate

        /// <summary>
        /// Leaving in La Lata (docs/PRUEBA-2026-10-14.md P1): one player calls it from
        /// the bus, the other answers, and whoever is outside when it leaves is lost.
        /// `clearing`: a Limpieza, where leaving is a retreat that loses the mission;
        /// `ready`: the objective is done (always so in a Saqueo); `left`: who would
        /// stay outside; `noSeat`: who is in the zone but has no seat, of `seats`
        /// (Arturo, 17/11: a Rescate's survivor takes one, and whoever doesn't fit is lost); `splitShort`: a Separados'
        /// group with nobody aboard, its living by name (Rules.SplitShort; G23.4), the reward lost if it went now.
        /// In an expedition: `stage`, its node's kind (RunFight.NodeKind); `boss`, the boss's name on its stage; `lost`, the
        /// reward already out of reach, so nothing is warned of but that (Rules.SplitLost; Mordido turned, El herido dead).
        /// </summary>
        public sealed class EvacSituation
        {
            public EvacSituation(bool clearing, bool ready, IReadOnlyList<string> left, IReadOnlyList<string>? noSeat = null, int seats = 0, IReadOnlyList<string>? splitShort = null, string? stage = null, string? boss = null, bool lost = false)
            {
                Clearing = clearing;
                Ready = ready;
                Left = left;
                NoSeat = noSeat ?? new string[0];
                Seats = seats;
                SplitShort = splitShort ?? new string[0];
                Stage = stage;
                Boss = boss;
                Lost = lost;
            }

            public bool Clearing { get; }
            public bool Ready { get; }
            public IReadOnlyList<string> Left { get; }
            public IReadOnlyList<string> NoSeat { get; }
            public int Seats { get; }
            public IReadOnlyList<string> SplitShort { get; }
            public string? Stage { get; }
            public string? Boss { get; }
            public bool Lost { get; }
        }

        /// <summary>
        /// What leaving now costs, in a sentence or two: the mission, and who stays outside. In an expedition (seats counted)
        /// it is what the run does with a fight lost with someone out (RunFights.ApplyFight; Lore, 20/11): the stage is passed
        /// without its reward, and without the recruit in Mordido and El herido; the boss's alive, the expedition is over.
        /// </summary>
        private static string EvacStakes(EvacSituation o)
        {
            var run = o.Stage != null || o.Seats > 0;
            var recruit = o.Stage == NodeKinds.Bitten || o.Stage == NodeKinds.Wounded;
            var split = !o.Clearing && !o.Lost && o.SplitShort.Count > 0;
            var lose = o.Clearing
                ? run ? "Si os vais ahora, la Limpieza queda a medias: la etapa se supera sin la recompensa." : "Si os vais ahora, la Limpieza queda a medias y la misión se pierde."
                : o.Lost
                    ? recruit ? "Ya no hay recluta ni recompensa." : "Ya no hay recompensa."
                    : o.Ready || split
                        ? ""
                        : o.Stage == NodeKinds.Boss
                            ? $"{o.Boss ?? "El jefe"} sigue en pie: si salís ahora, la expedición acaba y volvéis a la base."
                            : run
                                ? $"El objetivo no está cumplido: si salís ahora, la etapa se supera {(recruit ? "sin recluta ni recompensa" : "sin la recompensa")}."
                                : "El objetivo no está cumplido: si salís ahora, la misión se pierde.";
            // In an expedition (seats counted, T24), say why: not got on yet, or no seat left (Red and Producto, 18/11).
            var boarding = o.Seats > 0;
            // Nobody out: in an expedition those on its tiles get in as it goes (DepartEvac), not yet inside (UI, 20/11).
            var @out = o.Left.Count == 0
                ? o.NoSeat.Count == 0 ? boarding ? "Salís todos." : "Estáis todos dentro." : ""
                : o.Left.Count == 1
                    ? boarding ? $"{o.Left[0]} no ha subido: se quedaría fuera, y se perdería." : $"{o.Left[0]} se quedaría fuera, y se perdería."
                    : boarding ? $"{JoinAnd(o.Left)} no han subido: se quedarían fuera, y se perderían." : $"{JoinAnd(o.Left)} se quedarían fuera, y se perderían.";
            var seat = o.NoSeat.Count == 0
                ? ""
                : o.NoSeat.Count == 1
                    ? $"No queda plaza: {o.NoSeat[0]} se quedaría fuera, y se perdería."
                    : $"No queda plaza: {JoinAnd(o.NoSeat)} se quedarían fuera, y se perderían.";
            // Separados (G23.4): the group with nobody aboard, after who stays out; «su grupo» when they are just those, and
            // one left of it by name, «Sin Erik a bordo» (Producto, 20/11: «su grupo» of one is too much).
            var group = !split
                ? ""
                : o.SplitShort.Count > 1 && o.Left.Count == o.SplitShort.Count && o.SplitShort.All(o.Left.Contains)
                    ? "Sin nadie de su grupo a bordo, la etapa se supera sin la recompensa."
                    : $"Sin {SplitText.NobodyOf(o.SplitShort)} a bordo, la etapa se supera sin la recompensa.";
            return string.Join(" ", new[] { lose, @out, group, seat }.Where((x) => x.Length > 0));
        }

        /// <summary>The prompt on reaching the bus: its title, its text, and its two buttons.</summary>
        public sealed class EvacPromptText
        {
            public EvacPromptText(string title, string text, string go, string wait)
            {
                Title = title;
                Text = text;
                Go = go;
                Wait = wait;
            }

            public string Title { get; }
            public string Text { get; }
            public string Go { get; }
            public string Wait { get; }
        }

        /// <summary>
        /// The prompt on reaching the bus: leave or wait. `partner`: who has to accept, or null when playing alone.
        /// «Evacuar», the bar's verb, not «Salir» (Arturo, 18/11, T8: a «Salir» alone looked like quitting the game).
        /// </summary>
        public static EvacPromptText EvacPrompt(EvacSituation o, string? partner)
        {
            var title = o.Clearing ? "Retirada" : o.Ready || o.Lost ? $"Evacuar {Bus.In}" : o.SplitShort.Count > 0 ? SplitText.EvacTitle : "Evacuar sin el objetivo";
            var text = !string.IsNullOrEmpty(partner) ? $"{EvacStakes(o)} {partner} tiene que aceptar." : EvacStakes(o);
            return new EvacPromptText(title, text, o.Clearing ? "Retirarse" : !string.IsNullOrEmpty(partner) ? "Pedir evacuar" : "Evacuar", "Esperar");
        }

        /// <summary>The partner's notice: its title, its text, and its two buttons.</summary>
        public sealed class EvacAskText
        {
            public EvacAskText(string title, string text, string accept, string refuse)
            {
                Title = title;
                Text = text;
                Accept = accept;
                Refuse = refuse;
            }

            public string Title { get; }
            public string Text { get; }
            public string Accept { get; }
            public string Refuse { get; }
        }

        /// <summary>The partner's notice: `who` asks to leave; accept or wait.</summary>
        public static EvacAskText EvacAsk(string who, EvacSituation o)
        {
            return new EvacAskText(
                o.Clearing ? $"{who} pide retirarse" : $"{who} pide evacuar",
                EvacStakes(o),
                o.Clearing ? "Retirarse" : "Evacuar",
                "Esperar");
        }

        /// <summary>While the call waits for the partner's answer: «Esperando a Ana…».</summary>
        public static string EvacWaiting(string partner)
        {
            return $"Esperando a {partner}…";
        }

        /// <summary>The log line when the partner says no: «Ana quiere esperar.»</summary>
        public static string EvacRefusedLine(string who)
        {
            return $"{who} quiere esperar.";
        }

        /// <summary>The bus leaves: the log line, with who is in it.</summary>
        public static string EvacDepartLine(IReadOnlyList<string> @out)
        {
            return @out.Count > 0 ? $"{Bus.Name} sale con {JoinAnd(@out)}." : $"{Bus.Name} sale sin nadie.";
        }

        /// <summary>The log line as it leaves without those who didn't fit (Arturo, 17/11): «No caben todos: Marta Gil se queda fuera.»</summary>
        public static string EvacNoSeatLine(IReadOnlyList<string> noSeat)
        {
            return noSeat.Count == 1 ? $"No caben todos: {noSeat[0]} se queda fuera." : $"No caben todos: {JoinAnd(noSeat)} se quedan fuera.";
        }

        /// <summary>
        /// Getting on La Lata on reaching one of its tiles (Arturo, 18/11, T24): the soldier is safe inside, can get off on
        /// their turn to free the seat, and La Lata leaves with «Evacuar», or by itself once every living one is aboard.
        /// «Subir» and «bajar», not «entrar»: they hold for the motorbike too.
        /// </summary>
        public static class BoardText
        {
            /// <summary>Before moving, on the path's last tile.</summary>
            public const string WillBoard = "Subirá a " + Bus.Name;
            /// <summary>
            /// The objective with La Lata full and some of the squad still out (Lore, 18/11): go, or someone gets down. The
            /// Rescate's after who is aboard (VipObjective "full"), the Asedio's alone (Red's pass, 18/11).
            /// </summary>
            public const string FullObjective = Bus.Name + " va llena: evacuad o que baje alguien";
            /// <summary>No seat left: before moving, and as the refusal.</summary>
            public const string Full = "No queda plaza en " + Bus.Name + ".";
            /// <summary>On the bust (and the flag) of whoever is aboard: short, to fit whole («EVACUADO» was cut).</summary>
            public const string AboardTag = "A BORDO";
            /// <summary>The action to get off, and its help.</summary>
            public const string Alight = "Bajar";
            public const string AlightHelp = "Baja de " + Bus.Name + " a una casilla libre junto a sus puertas y deja libre su plaza. Le queda la otra acción y todo su movimiento. Fuera le pueden ver y atacar.";
            /// <summary>The log lines.</summary>
            public static string Boarded(string who) => $"{who} sube a {Bus.Name}: está a salvo.";
            public static string Alighted(string who) => $"{who} baja de {Bus.Name}.";
            /// <summary>The log when it leaves by itself, every living one aboard (EvacDepartLine follows).</summary>
            public const string AllAboard = "Ya están todos dentro: " + Bus.Name + " sale.";
        }

        /// <summary>The words for evacuating that don't depend on who.</summary>
        public static class EvacText
        {
            /// <summary>Log, when a call nobody answered lapses with the squad's turn.</summary>
            public const string Lapsed = "Nadie ha contestado: la petición de salir caduca con el turno.";
            /// <summary>The evacuation notice's kicker, and the EVACUAR button's tooltip.</summary>
            public const string Kicker = Bus.Name;
            public const string ButtonTip = "Evacuar " + Bus.In + ": sale con los que estén dentro, y quien esté fuera se pierde";
            /// <summary>The debrief's status of someone left outside (`lost`).</summary>
            public const string Lost = "Se quedó fuera";
            /// <summary>The mission's end when the bus left early: a Limpieza abandoned, or any other without its objective.</summary>
            public const string Retreat = "Retirada";
            public const string WithoutObjective = "Salisteis sin el objetivo";
        }

        /// <summary>
        /// A mission against the clock (playtest 18/10 #8–9): at the end of its last
        /// turn the bus leaves by itself with whoever is in the zone, and those
        /// outside are lost. The outcomes are headlines without a full stop.
        /// </summary>
        public static class TimeText
        {
            /// <summary>At the start of the last turn: the strip's alert and the log.</summary>
            public const string LastTurn = "Último turno: " + Bus.Name + " sale al acabarlo, con los que estén dentro.";
            public const string LastTurnFloat = "ÚLTIMO TURNO";
            /// <summary>Log, as it leaves on time.</summary>
            public const string Departs = Bus.Name + " no espera más.";
            /// <summary>Outcomes: on time with the objective aboard (a win), without it (`timer`), with nobody aboard, and a mission with no bus.</summary>
            public const string WonOnTime = "Se acabó el tiempo, pero " + Bus.Name + " salió con el objetivo a bordo";
            public const string LostOnTime = "Se acabó el tiempo: " + Bus.Name + " se fue sin cumplir el objetivo";
            public const string LeftEmpty = "Se acabó el tiempo: " + Bus.Name + " se fue sin nadie";
            public const string TimeUp = "Se acabó el tiempo";
        }

        /// <summary>With `n` turns left (5, playtest 18/10 #9): «Quedan 5 turnos. Al acabar el último, La Lata se va.»</summary>
        public static string TurnsLeftWarning(int n)
        {
            return $"Quedan {n} turnos. Al acabar el último, {Bus.Name} se va.";
        }

        /// <summary>
        /// With `n` turns left to do the objective (3; Ana's playtest, 28/10), before the bus leaves without it or the time
        /// runs out: «Quedan 3 turnos para cumplir el objetivo, o la misión se pierde.» Not in lore.ts.
        /// </summary>
        public static string TurnsToLoseWarning(int n)
        {
            return n == 1 ? "Queda 1 turno para cumplir el objetivo, o la misión se pierde." : $"Quedan {n} turnos para cumplir el objetivo, o la misión se pierde.";
        }

        /// <summary>The same, floating, as TimeText.LastTurnFloat: «QUEDAN 3 TURNOS». Not in lore.ts.</summary>
        public static string TurnsLeftFloat(int n)
        {
            return n == 1 ? "QUEDA 1 TURNO" : $"QUEDAN {n} TURNOS";
        }

        /// <summary>
        /// The countdown on a proposal nobody answers (docs/PRUEBA-2026-10-14.md #28):
        /// when it runs out, it is accepted. `left` comes formatted («1:25»); `mine`:
        /// shown to whoever proposed it.
        /// </summary>
        public static string ProposalClock(string left, bool mine)
        {
            return mine ? $"Si no contesta, se acepta sola en {left}" : $"Sin respuesta, se acepta sola en {left}";
        }

        /// <summary>The same for a call to leave: when it runs out, the bus goes.</summary>
        public static string EvacClock(string left, bool mine)
        {
            return mine ? $"Si no contesta, {Bus.Name} sale en {left}" : $"Sin respuesta, {Bus.Name} sale en {left}";
        }

        /// <summary>
        /// Leaving from the action bar, without a window (docs/PRUEBA-2026-10-15.md
        /// #10): the partner's button, the card of «Esperar», and the notice.
        /// </summary>
        public static class EvacBar
        {
            public const string Accept = "Aceptar evacuación";
            public const string Wait = "Esperar";
            /// <summary>The card when the partner chooses to wait.</summary>
            public const string Stay = Bus.Name + " no sale todavía: seguís en la misión.";
        }

        /// <summary>The notice to the partner: «Ana pide evacuar: «Aceptar evacuación» o «Esperar», en tu barra.»</summary>
        public static string EvacBarNotice(string who)
        {
            return $"{who} pide evacuar: «{EvacBar.Accept}» o «{EvacBar.Wait}», en tu barra.";
        }

        /// <summary>
        /// Leaving a mission early, asked of the partner like the bus (playtest 16/10):
        /// whoever is still standing comes home, and the mission is lost.
        /// </summary>
        public static class AbandonText
        {
            /// <summary>The button, and what it costs, for whoever asks.</summary>
            public const string Button = "Abandonar la misión";
            public const string Stakes = "Los que sigan en pie vuelven a la base, pero la misión se da por perdida.";
            /// <summary>
            /// On an expedition it is only a safety net (cuadro 122 and Producto's review, 23/11: Rules.AbandonAllowed, no La Lata
            /// or a soldier with no way to it): why it is offered, under the menu's button, and what it costs (nobody falls: the
            /// standing go on with the expedition and what they carry; the stage without its reward).
            /// </summary>
            public static readonly string NoWay = "No hay forma de llegar " + Bus.To + ": podéis abandonar sin perder a nadie.";
            public const string StakesRun = "Salís todos con lo que lleváis, y la expedición sigue. La etapa queda sin la recompensa.";
            public const string Accept = "Abandonar";
            public const string Refuse = "Seguir";
        }

        /// <summary>The partner's notice: «Arturo quiere abandonar la misión».</summary>
        public static string AbandonAsk(string who)
        {
            return $"{who} quiere abandonar la misión";
        }

        /// <summary>The notice to the partner, as the bus's: «Arturo quiere abandonar la misión: «Abandonar» o «Seguir», en tu barra.»</summary>
        public static string AbandonBarNotice(string who)
        {
            return $"{AbandonAsk(who)}: «{AbandonText.Accept}» o «{AbandonText.Refuse}», en tu barra.";
        }

        /// <summary>The log line when the partner says no: «Arturo quiere seguir.»</summary>
        public static string AbandonRefusedLine(string who)
        {
            return $"{who} quiere seguir.";
        }

        /// <summary>The log line for someone outside when the bus leaves: «Ana se queda fuera.»</summary>
        public static string LeftBehindLine(string who)
        {
            return $"{who} se queda fuera.";
        }
    }
}
