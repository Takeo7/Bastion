namespace Bastion.UI.Start
{
    /// <summary>
    /// The start screen's words, as the Electron app's launcher.html had them (0.10.1), plus the name, asked here now
    /// (Lore reviews them, docs/UNITY-UI.md §6). Red's NetLink gives its own texts for what fails (Failed).
    /// </summary>
    public static class StartText
    {
        public const string Title = "BASTIÓN";
        public const string Kicker = "TÁCTICA POR TURNOS COOPERATIVA";
        public const string NameLabel = "TU NOMBRE";
        public const string NameMissing = "Escribe tu nombre.";
        public const string Host = "SER ANFITRIÓN";
        public const string HostAbout = "La partida se abre en este ordenador. Quien juegue contigo entra desde su app.";
        public const string LanLabel = "PARTIDAS EN TU RED";
        public const string LanSearching = "Buscando partidas en tu red…";
        public const string LanNone = "Ninguna por ahora. Cuando el otro jugador pulse «Ser anfitrión», saldrá aquí.";
        public const string JoinLabel = "UNIRSE POR DIRECCIÓN";
        /// <summary>The host's address is on its main menu once the game is open (lobby.ts invite, and so in Unity).</summary>
        public const string JoinHint = "Por Tailscale, o si la partida no sale arriba: escribe la dirección que aparece en el menú principal de quien la aloja.";
        public const string JoinPlaceholder = "192.168.1.20:41391";
        public const string Join = "UNIRSE";
        public const string Quit = "SALIR DE BASTIÓN";
        public const string Opening = "Abriendo la partida…";
        public const string Looking = "Buscando la partida…";

        /// <summary>What a game on the network is doing (LanGame.Phase).</summary>
        public static string Phase(string phase) => phase == "lobby" ? "en el menú" : phase == "base" ? "en la base" : phase == "mission" ? "en misión" : phase;

        public static string GameOf(string name) => $"Partida de {name}";
        public static string Entering(string name) => $"Entrando en la partida de {name}…";
        public static string Players(int n) => n == 1 ? "1 jugador" : $"{n} jugadores";
        public static string OtherVersion(string theirs, string mine) => $"Tiene Bastión {theirs} y tú, {mine}: necesitáis la misma.";
        public static string Version(string v) => $"versión {v}";
    }
}
