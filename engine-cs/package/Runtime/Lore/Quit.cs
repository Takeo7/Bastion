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

        /// <summary>
        /// Joining when the host can't be reached (cuadro 172, Arturo 26/11: say why and what to touch; brought to the
        /// 0.12.27 in cuadro 183). NetLink picks one by ClientSession.Unreached's cause. `where` is the address tried,
        /// «192.168.1.20:41391»; the buttons and addresses are named as the 0.12 start screen and room show them.
        /// </summary>
        public static class JoinText
        {
            /// <summary>
            /// No route at once on a Mac: the app without «Red local». The 0.12 line is listed as «Bastión», without its
            /// number, each one installed over the last; the 0.14's («Bastión 0.14.13»…) carry the same identifier and
            /// confuse macOS (Red, 27/11).
            /// </summary>
            public const string NoLocalNetworkMac = "Este Mac no deja a Bastión usar la red local: en Ajustes del Sistema › Privacidad y seguridad › Red local, activa «Bastión». Si en la lista salen otras, como «Bastión 0.14.13», bórralas de Aplicaciones.";
            /// <summary>The same on iOS: `device` is «iPhone» or «iPad».</summary>
            public static string NoLocalNetworkIos(string device) => $"Este {device} no deja a Bastión usar la red local: en Ajustes › Privacidad y seguridad › Red local, activa Bastión.";
            /// <summary>No route anywhere else (Windows): no network that leads there.</summary>
            public static string NoRoute(string where) => $"No hay camino hasta {where} desde aquí. Mirad que los dos estéis en la misma wifi; desde otra red, usad la dirección «Por internet, con la clave» o la de «Con Tailscale».";
            /// <summary>Refused: nobody listens on that port, the game not open or another port.</summary>
            public static string Refused(string where) => $"En {where} no hay ninguna partida abierta. Que quien aloja pulse «Alojar partida», y copiad la dirección entera de su sala.";
            /// <summary>No answer in the time: a firewall or a VPN between, or not that address.</summary>
            public static string NoAnswer(string where) => $"No responde nadie en {where}. Mirad que la dirección sea la de su sala, que quien aloja haya permitido a Bastión en su cortafuegos y que ninguno de los dos tenga una VPN puesta. Desde otra red, usad la dirección «Por internet, con la clave» o la de «Con Tailscale».";
        }
    }
}
