using System.Collections.Generic;

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
        /// stay outside.
        /// </summary>
        public sealed class EvacSituation
        {
            public EvacSituation(bool clearing, bool ready, IReadOnlyList<string> left)
            {
                Clearing = clearing;
                Ready = ready;
                Left = left;
            }

            public bool Clearing { get; }
            public bool Ready { get; }
            public IReadOnlyList<string> Left { get; }
        }

        /// <summary>What leaving now costs, in a sentence or two: the mission, and who stays outside.</summary>
        private static string EvacStakes(EvacSituation o)
        {
            var lose = o.Clearing
                ? "Si os vais ahora, la Limpieza queda a medias y la misión se pierde."
                : !o.Ready
                    ? "El objetivo no está cumplido: si salís ahora, la misión se pierde."
                    : "";
            var @out = o.Left.Count == 0
                ? "Estáis todos dentro."
                : o.Left.Count == 1
                    ? $"{o.Left[0]} se quedaría fuera, y se perdería."
                    : $"{JoinAnd(o.Left)} se quedarían fuera, y se perderían.";
            return lose.Length > 0 ? $"{lose} {@out}" : @out;
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

        /// <summary>The prompt on reaching the bus: leave or wait. `partner`: who has to accept, or null when playing alone.</summary>
        public static EvacPromptText EvacPrompt(EvacSituation o, string? partner)
        {
            var title = o.Clearing ? "Retirada" : o.Ready ? $"Salir {Bus.In}" : "Salir sin el objetivo";
            var text = !string.IsNullOrEmpty(partner) ? $"{EvacStakes(o)} {partner} tiene que aceptar." : EvacStakes(o);
            return new EvacPromptText(title, text, o.Clearing ? "Retirarse" : !string.IsNullOrEmpty(partner) ? "Pedir salir" : "Salir", "Esperar");
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
                o.Clearing ? $"{who} pide retirarse" : $"{who} pide salir",
                EvacStakes(o),
                o.Clearing ? "Retirarse" : "Salir",
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

        /// <summary>The words for evacuating that don't depend on who.</summary>
        public static class EvacText
        {
            /// <summary>Log, when a call nobody answered lapses with the squad's turn.</summary>
            public const string Lapsed = "Nadie ha contestado: la petición de salir caduca con el turno.";
            /// <summary>The evacuation notice's kicker, and the EVACUAR button's tooltip.</summary>
            public const string Kicker = Bus.Name;
            public const string ButtonTip = "Salir " + Bus.In + ": quien esté fuera cuando salga se pierde";
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
            public const string LastTurn = "Último turno: " + Bus.Name + " sale al acabarlo, con quien esté en la zona de evacuación.";
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

        /// <summary>The notice to the partner: «Ana pide salir: «Aceptar evacuación» o «Esperar», en tu barra.»</summary>
        public static string EvacBarNotice(string who)
        {
            return $"{who} pide salir: «{EvacBar.Accept}» o «{EvacBar.Wait}», en tu barra.";
        }

        /// <summary>
        /// The bite in the field (docs/PRUEBA-2026-10-15.md #13): the tag, its note,
        /// the floating word and the log line. A noun, not «mordido», so it agrees with
        /// everyone.
        /// </summary>
        public const string BiteTag = "MORDISCO";
        public const string BiteFloat = "MORDISCO −1";

        /// <summary>Why someone bitten can't board without a Cuarentena: the button's reason. The same words are written in rules.ts (the evac refusal) and test/prueba15.test.ts: change the three together.</summary>
        public const string BiteNoBoard = "Con un mordisco no se sube a " + Bus.Name + " sin una Cuarentena en el Bastión.";

        /// <summary>The tag's note, on the sheet and the list: what it does now and what it will need.</summary>
        public static string BiteNote(bool quarantine)
        {
            return $"Mordisco: pierde 1 de salud al empezar cada turno y, si cae, se levanta. {(quarantine ? "En la base necesitará el tratamiento." : $"Sin Cuarentena en el Bastión no sube a {Bus.Name}.")}";
        }

        /// <summary>The log line each turn: «Ana pierde 1 de salud por el mordisco.»</summary>
        public static string BiteLossLine(string who)
        {
            return $"{who} pierde 1 de salud por el mordisco.";
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
