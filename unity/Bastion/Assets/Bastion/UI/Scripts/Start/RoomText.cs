using Bastion.Engine;
using Bastion.Net;

namespace Bastion.UI.Start
{
    /// <summary>
    /// The room menu's words (lobby.ts of the 0.10.1; Lore reviews them): who is in, where the partner joins, the
    /// campaign to go on with or a new one, and the skirmish's sheet (the mission, the map, day or night, start).
    /// </summary>
    public static class RoomText
    {
        public const string Commanders = "Comandantes";
        public const string Connecting = "Conectando…";
        public const string WaitingPlayer = "Esperando jugador…";
        public const string Connected = "Conectado";
        public const string Offline = "Desconectado";
        public const string Away = "Ausente";
        public const string Invite = "Quien juegue contigo te verá en «Partidas en tu red» de su app. Si no te ve, que use «Unirse por dirección» con:";
        /// <summary>Each of the host's addresses, by what it is (net.ts inviteLinks of the 0.10.1).</summary>
        public const string Lan = "En tu misma red";
        public const string Tailscale = "Por internet (Tailscale)";
        public const string Copy = "Copiar";
        public const string Copied = "Copiada";
        public const string Mission = "Misión";
        public const string Map = "Mapa";
        public const string Hour = "Hora";
        public const string Day = "De día";
        public const string DayAbout = "Como siempre";
        public const string Night = "De noche";
        public const string NightAbout = "Se ve poco, salvo bajo las farolas. Más muertos y más botín";
        public const string StartAlone = "Empezar en solitario";
        public const string StartAloneAbout = "Controlarás a todos los soldados";
        public const string StartTwo = "Empezar escaramuza";
        public const string StartTwoAbout = "Cada uno manda a la mitad";
        /// <summary>Back on the start screen because the same seat was opened from another window or app of this computer (Lore, 23/10).</summary>
        public const string Replaced = "Has abierto la partida en otra ventana, y esta se ha desconectado.";

        public static string OtherAddresses(int n) => $"Otras direcciones ({n})";

        // The main page and a new campaign's question (lobby.ts renderMain, renderConfirm).
        public const string Continue = "Continuar campaña";
        public const string NewCampaign = "Nueva campaña";
        public const string NewCampaignAbout = "La base compartida, de principio a fin";
        public const string Skirmish = "Escaramuza";
        public const string SkirmishAbout = "Una misión suelta, sin consecuencias";
        public const string SkirmishBlocked = "Antes, acabad la misión de la campaña";
        public const string Back = "Volver";
        public const string AskAgain = "¿Empezar de cero?";
        public const string AskAlone = "¿Empezar sin el otro jugador?";
        public const string AloneText = "El otro jugador aún no ha entrado. Puedes empezar igualmente: cuando entre, tomará el mando de 4 soldados.";
        public const string Cancel = "Cancelar";
        public const string Wait = "Esperar";
        public const string StartAgain = "Empezar de cero";
        public const string StartNew = "Empezar";

        /// <summary>«8 salidas · Nivel 3 · Marea 5/12», or the mission left half played (lobby.ts renderMain).</summary>
        public static string Saved(SavedCampaign s)
        {
            if (s.InMission != null) return $"Misión a medias: {s.InMission}";
            var outings = Lore.OutingsMark(s.Missions);
            return $"{outings.Substring(0, 1)}{outings.Substring(1).ToLowerInvariant()} · Nivel {s.Level} · {Lore.DoomName(s.Level)} {s.Doom}/12";
        }

        /// <summary>The saved campaign the new one replaces (renderConfirm).</summary>
        public static string Replaces(SavedCampaign s) => $"La campaña guardada ({Lore.OutingsMark(s.Missions).ToLowerInvariant()}) se sustituye por una nueva.";

        /// <summary>A campaign from before the 0.7 kept aside (setAsideNote).</summary>
        public static string SetAside(string file)
        {
            var release = System.Text.RegularExpressions.Regex.Match(file ?? "", @"antes-(\d+\.\d+)");
            return $"Vuestra campaña anterior está guardada aparte, en {file}: {(release.Success ? $"con la {release.Groups[1].Value}" : "con esta versión")} empieza una nueva.";
        }

        /// <summary>The maps of the first milestone: the city alone (lobby.ts MAPS).</summary>
        public static readonly (string Id, string Name, string About)[] Maps = { ("city", "Ciudad", "Edificios, azoteas y calles") };

        /// <summary>
        /// The missions: their names (setup.ts MISSION_NAMES, from the engine's Setup once it is in C#) and what each is
        /// (lobby.ts MISSIONS).
        /// </summary>
        public static readonly (string Id, string Name, string About)[] Missions =
        {
            ("elimination", "Limpieza", "Abatid a todos los hostiles del mapa."),
            ("recovery", "Botín", "Recuperad la carga y salid " + Lore.Bus.In + " antes de que se acabe el tiempo."),
            ("sabotage", "Sabotaje", "Apagad la alarma antes de que llegue la horda."),
            ("hack", "Arreglar la radio", "Arreglad la radio. Si falla, salta la alarma."),
            // Not «el VIP»: neither an English acronym nor always a man (Lore, 23/10).
            ("rescue", "Rescate", "Llegad hasta la persona retenida y sacadla con vida."),
            ("scavenge", "Saqueo", "Registrad las casas y salid con lo que podáis: cada ruido trae más muertos."),
        };
    }
}
