namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ quit

        /// <summary>
        /// Leaving the game from the desktop app (the Esc menu in a mission, the
        /// base's menu, the lobby), named like the app's own menu. The room saves
        /// after every action, so nothing is lost; what changes is the partner's side.
        /// </summary>
        public static class QuitText
        {
            public const string Button = "Salir de Bastión";
            public const string Title = "¿Salir de Bastión?";
            public const string Saved = "La partida queda guardada tal como está.";
            public const string SavedMission = "La partida queda guardada tal como está, con la misión a medias.";
            public const string Accept = "Salir";
            public const string Refuse = "Seguir";
        }

        /// <summary>For the host, with the partner connected: the server stops with the app. «La partida está en tu ordenador: si sales, Ana se desconecta hasta que vuelvas a abrirla.»</summary>
        public static string QuitHostWarning(string partner)
        {
            return $"La partida está en tu ordenador: si sales, {partner} se desconecta hasta que vuelvas a abrirla.";
        }

        /// <summary>For the guest in a mission, with the host connected: the turns go on, and an absent player's soldiers never act.</summary>
        public static string QuitGuestWarning(string partner)
        {
            return $"{partner} sigue la misión sin ti: tus soldados se quedan quietos donde están hasta que vuelvas.";
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
