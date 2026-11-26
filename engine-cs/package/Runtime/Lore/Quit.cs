namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ quit

        /// <summary>
        /// Leaving the game from the desktop app (the Esc menu in a mission, the
        /// base's menu, the lobby): «Salir», as the start menu's (Arturo, cuadro 171:
        /// fewer words). The room saves after every action, so nothing is lost; what
        /// changes is the partner's side.
        /// </summary>
        public static class QuitText
        {
            public const string Button = "Salir";
            public const string Title = "¿Salir?";
            public const string Saved = "La partida queda guardada tal como está.";
            public const string SavedMission = "La partida queda guardada tal como está, con la misión a medias.";
            public const string Accept = "Salir";
            public const string Refuse = "Seguir";
        }

        /// <summary>For the host, with the partner connected: the server stops with the app. «Alojas tú la partida: si sales, Ana se desconecta hasta que vuelvas a abrirla.» Not «en tu ordenador»: on iOS it is an iPad or an iPhone (Lore, 7/11).</summary>
        public static string QuitHostWarning(string partner)
        {
            return $"Alojas tú la partida: si sales, {partner} se desconecta hasta que vuelvas a abrirla.";
        }

        /// <summary>For the guest in a mission, with the host connected: the turns go on, and the host commands an absent player's soldiers (Content.CommandsFor, since 17/10).</summary>
        public static string QuitGuestWarning(string partner)
        {
            return $"{partner} sigue la misión sin ti, y manda en tus soldados hasta que vuelvas.";
        }

        /// <summary>For the guest when the host's game closes (client net.ts): the notice, its second line and, in a browser, the button.</summary>
        public static class HostClosed
        {
            public const string Back = "Cuando la vuelva a abrir, podrás entrar de nuevo.";
            public const string Retry = "Volver a entrar";
            /// <summary>«Volver a entrar» with the game still closed.</summary>
            public const string NotYet = "Aún no está abierta.";
            /// <summary>In the app: back to its start screen.</summary>
            public const string Home = "Ir al inicio";
        }

        /// <summary>«Arturo ha cerrado la partida.»; without the host's name, a neutral one.</summary>
        public static string HostClosedLine(string? host = null)
        {
            return !string.IsNullOrEmpty(host) ? $"{host} ha cerrado la partida." : "Quien aloja la partida la ha cerrado.";
        }

        /// <summary>Back to the window (minimised, or the laptop asleep) while the dead socket comes back (client net.ts).</summary>
        public const string RejoinLine = "Volviendo a la partida…";
    }
}
