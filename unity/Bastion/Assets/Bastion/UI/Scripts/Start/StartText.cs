using Bastion.Client.Net;

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

        // The main menu (p1), in this order; «Continuar» only with a game saved. (Arturo, 26/11: fewer words, «Salir», not
        // «Salir de Bastión»; Producto's table, Lore to review.)
        public const string Continue = "Continuar";
        /// <summary>Under it, the saved game's chapter and level (cuadro 125).</summary>
        public static string SavedLine(int chapter, string level) => $"Capítulo {System.Math.Max(1, chapter)} · {level}";
        /// <summary>Where «Continuar partida» would be, with only a campaign from before the expeditions (NetLink.OldCampaign; Lore, 16/11).</summary>
        public static string OldCampaign(string file) => $"La campaña anterior no se puede continuar con esta versión. Queda guardada aparte, en {file}. Con «Nueva partida» empiezas la de las expediciones.";
        public const string New = "Nueva partida";
        public const string Two = "Multijugador";
        public const string Settings = "Ajustes";
        public const string Quit = "Salir";
        public const string Back = "Volver";

        // «Multijugador» (p2): to host; to join the game heard on the network, or by address when there is none
        // (Arturo's choice, 14/11).
        public const string Host = "Alojar";
        /// <summary>In the infinitive as «Unirse por dirección» and «Alojar» (Lore, 23/11; Producto's review: two forms of the verb side by side).</summary>
        public static string JoinOf(string name) => $"Unirse a {name}";
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

        /// <summary>«iPad» or «iPhone», what this one is (SystemInfo.deviceModel: «iPad13,18», «iPhone17,1»).</summary>
        public static string Device => UnityEngine.SystemInfo.deviceModel.StartsWith("iPhone") ? "iPhone" : "iPad";

        /// <summary>«Se guardan en este ordenador» (the settings, on the base's menu and the start screen's), or on this iPad or iPhone.</summary>
        public static string SettingsHere => NetLink.LanBlind ? $"Se guardan en este {Device}." : "Se guardan en este ordenador.";
    }
}
