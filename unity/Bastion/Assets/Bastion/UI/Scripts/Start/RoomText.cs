using Bastion.Engine;

namespace Bastion.UI.Start
{
    /// <summary>
    /// The room menu's words (lobby.ts of the 0.10.1, its skirmish sheet; Lore reviews them), for the first milestone:
    /// who is in, where the partner joins, the mission, the map (the city), day or night, start. No campaign yet.
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
