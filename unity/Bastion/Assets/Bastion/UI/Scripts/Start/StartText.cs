namespace Bastion.UI.Start
{
    /// <summary>
    /// The start screen's words (Arturo's notes of 14/11, menu-inicial; 0.12.24): the main menu's keys, «Partida
    /// multijugador»'s two cards, and what is asked (the name, an address and its key); no help under them (Lore reviews
    /// them, docs/UNITY-UI.md §6). Red's NetLink gives its own texts for what fails (Failed).
    /// </summary>
    public static class StartText
    {
        public const string Title = "BASTIÓN";

        // The main menu (p1), in this order; «Continuar partida» only with a game saved.
        public const string Continue = "Continuar partida";
        public const string New = "Nueva partida";
        public const string Two = "Partida multijugador";
        public const string Settings = "Ajustes";
        public const string Quit = "Salir de Bastión";
        public const string Back = "Volver";

        // «Partida multijugador» (p2): to host; to join the game heard on the network, or by address when there is none
        // (Arturo's choice, 14/11). By address always since the 0.12.27: with a game heard, a key under its card (cuadro 183).
        public const string Host = "Alojar partida";
        public static string JoinOf(string name) => $"Unirte a la partida de {name}";
        public const string ByAddress = "Unirse por dirección";

        // What is asked: the name (before a new game; before anything else while there is none), an address and its key.
        // Under the name, the verb of what was pressed (Lore, 15/11: «Seguir» is staying, in «¿Salir de Bastión?»).
        public const string NameLabel = "TU NOMBRE";
        public const string NameMissing = "Escribe tu nombre.";
        public const string Start = "Empezar";
        public const string ContinueGo = "Continuar";
        public const string HostGo = "Alojar";
        public const string AddressLabel = "DIRECCIÓN";
        public const string JoinPlaceholder = "192.168.1.20:41391";
        /// <summary>The key is always on the host's screen: needed only where the game asks it (Lore, 15/11).</summary>
        public const string KeyLabel = "CLAVE, SI LA PIDE";
        public const string KeyPlaceholder = "KXRT5";
        public const string Join = "Unirse";

        // While it goes.
        public const string Opening = "Abriendo la partida…";
        public const string Looking = "Buscando la partida…";
        public static string Entering(string name) => $"Entrando en la partida de {name}…";
        public static string Version(string v) => $"versión {v}";

        /// <summary>Under Ajustes' title, on the start screen.</summary>
        public const string SettingsHere = "Se guardan en este ordenador.";
    }
}
