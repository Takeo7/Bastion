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

        /// <summary>
        /// Joining when the host can't be reached (cuadro 172, Arturo 26/11: say why and what to touch; NetLink, by
        /// ClientSession.Unreached's cause). `where` is the address tried, «192.168.1.20:41391».
        /// </summary>
        public static class JoinText
        {
            /// <summary>
            /// No route at once on a Mac: the app without «Red local» (macOS confuses it with several copies, and every
            /// version is a new app: Red, 26/11). `version` names the app as macOS lists it, «Bastión 0.14.12».
            /// </summary>
            public static string NoLocalNetworkMac(string version) => $"Este Mac no deja a Bastión usar la red local: en Ajustes del Sistema › Privacidad y seguridad › Red local, activa «Bastión {version}». Si salen otras versiones de Bastión, bórralas de Aplicaciones.";
            /// <summary>The same on iOS: `device` is «iPhone» or «iPad».</summary>
            public static string NoLocalNetworkIos(string device) => $"Este {device} no deja a Bastión usar la red local: en Ajustes › Privacidad y seguridad › Red local, activa Bastión.";
            /// <summary>No route anywhere else (Windows): no network that leads there.</summary>
            public static string NoRoute(string where) => $"No hay camino hasta {where} desde aquí. Mirad que los dos estéis en la misma wifi; desde otra red, usad la dirección «Por internet» o la de «Con Tailscale».";
            /// <summary>Refused: nobody listens on that port, the game not open or another port.</summary>
            public static string Refused(string where) => $"En {where} no hay ninguna partida abierta. Que quien aloja pulse «Alojar», y copiad la dirección entera de su sala.";
            /// <summary>No answer in the time (NetLink.ConnectTimeoutMs): a firewall or a VPN between, or not that address.</summary>
            public static string NoAnswer(string where) => $"No responde nadie en {where}. Mirad que la dirección sea la de su sala, que quien aloja haya permitido a Bastión en su cortafuegos y que ninguno de los dos tenga una VPN puesta. Desde otra red, usad la dirección «Por internet» o la de «Con Tailscale».";
        }
    }
}
