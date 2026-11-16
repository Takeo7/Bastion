using System.Linq;
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
        public const string Connected = "En línea";
        public const string Offline = "Sin conexión";
        public const string Away = "Ausente";
        /// <summary>
        /// How the partner finds this game (Lore, 15/11: as their card in «Partida multijugador» says it, with the host's
        /// name); without the name, in general.
        /// </summary>
        public static string Invite(string name) => string.IsNullOrEmpty(name)
            ? "Quien juegue contigo verá tu partida en «Partida multijugador» de su app. Si no la ve, que pulse «Unirse por dirección» y escriba:"
            : $"Quien juegue contigo verá «Unirte a la partida de {name}» en «Partida multijugador». Si no le sale, que pulse «Unirse por dirección» y escriba:";
        /// <summary>On iOS the game isn't announced on the network (NetLink.LanBlind): the address is the only way in (Lore, 7/11).</summary>
        public const string InviteBlind = "Quien juegue contigo tiene que escribir en «Unirse por dirección» de su app:";
        /// <summary>Each of the host's addresses, by what it is (net.ts inviteLinks of the 0.10.1).</summary>
        public const string Lan = "En tu misma red";
        /// <summary>«Con Tailscale» (Lore, 01/11: «Por internet» is the router's alone now).</summary>
        public const string Tailscale = "Con Tailscale";
        /// <summary>The router's port opened (Red's RouterPort; Arturo, 01/11): the host's address on the internet with the room's key (Lore).</summary>
        public const string Internet = "Por internet, con la clave";
        /// <summary>
        /// The room's key without the internet's address (Red, 0.12.17): asked of whoever comes from the internet, and from
        /// the house while the router's port is open (Gameplay; Lore, .dev-snapshots/lore/0.12.17/TEXTOS.md §2).
        /// </summary>
        public static string KeyLine(string key) => $"Clave de la partida: {key}. Se pide al entrar por internet, y también desde tu red si el puerto del router está abierto.";
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

        /// <summary>
        /// How the partner joins, for the game's menu (Esc) in the base and in the mission: hosting while they aren't in
        /// (Gameplay, 14/11), as DrawInvite in the room's menu: the first of the host's addresses and any by Tailscale, the
        /// internet's with the key once the router's port is open, else why not, and the key's line. Null otherwise.
        /// </summary>
        public static Bastion.UI.Hud.InviteView InviteOf(Bastion.Client.Net.NetLink link)
        {
            if (link == null || !link.Hosting || link.PartnerConnected) return null;
            var addresses = link.Addresses;
            var router = link.Router;
            var internet = router == RouterState.Open ? link.Invite ?? link.Internet : null;
            if (addresses.Count == 0 && internet == null) return null;
            var name = link.LastRoom?.Players.FirstOrDefault(p => p.Slot == link.Slot)?.Name ?? link.LastName;
            var v = new Bastion.UI.Hud.InviteView { Line = Bastion.Client.Net.NetLink.LanBlind ? InviteBlind : Invite(name), Copy = Copy, Copied = Copied };
            for (var i = 0; i < addresses.Count; i++)
                if (i == 0 || addresses[i].Kind == "tailscale") v.Addresses.Add((addresses[i].Kind == "tailscale" ? Tailscale : Lan, addresses[i].Address));
            if (internet != null) v.Addresses.Add((Internet, internet));
            else if (router != null && router != RouterState.Open) v.Router = RouterLine(router.Value);
            if (link.Key is string key) v.Key = KeyLine(key);
            return v;
        }

        /// <summary>The router's port while it opens, or why it won't (Lore's words, 01/11).</summary>
        public static string RouterLine(RouterState state) => state switch
        {
            RouterState.Trying => "Abriendo el puerto del router…",
            RouterState.NoRouter => "El router no deja abrir el puerto desde la app. Para jugar por internet, usad Tailscale los dos.",
            RouterState.Shared => "Tu compañía de internet comparte tu dirección con otros clientes, y desde fuera no se llega a tu router. Para jugar por internet, usad Tailscale los dos.",
            RouterState.Refused => "El router no ha abierto el puerto. Para jugar por internet, usad Tailscale los dos.",
            _ => null,
        };

        // The main page and a new campaign's question (lobby.ts renderMain, renderConfirm).
        public const string Continue = "Continuar campaña";
        public const string NewCampaign = "Nueva campaña";
        public const string NewCampaignAbout = "La base compartida, de principio a fin";
        public const string Skirmish = "Escaramuza";
        public const string SkirmishAbout = "Una misión suelta, sin consecuencias";
        public const string SkirmishBlocked = "Antes, acabad la etapa a medias";
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
            // The runs (Red, 16/11: Chapter is 0 in a campaign of before; Lore's words, TEXTOS-GUARDADO.md).
            if (s.Chapter > 0)
            {
                if (s.InMission != null) return $"Capítulo {s.Chapter} · Etapa a medias: {s.InMission}";
                var where = s.Row == null ? "En la base" : s.Row == 0 ? "Antes de la etapa 1" : $"Etapa {s.Row} de 7";
                return $"Capítulo {s.Chapter} · {where} · {(s.Soldiers == 1 ? "1 soldado" : $"{s.Soldiers} soldados")} · {s.Supplies} §";
            }
            if (s.InMission != null) return $"Misión a medias: {s.InMission}";
            var outings = Lore.OutingsMark(s.Missions);
            return $"{outings.Substring(0, 1)}{outings.Substring(1).ToLowerInvariant()} · Nivel {s.Level} · {Lore.DoomName(s.Level)} {s.Doom}/12";
        }

        /// <summary>The saved campaign the new one replaces (renderConfirm).</summary>
        public static string Replaces(SavedCampaign s) => s.Chapter > 0
            ? $"La partida guardada (capítulo {s.Chapter}, {(s.NodesPlayed == 0 ? "sin etapas jugadas" : s.NodesPlayed == 1 ? "1 etapa jugada" : $"{s.NodesPlayed} etapas jugadas")}) se sustituye por una nueva."
            : $"La campaña guardada ({Lore.OutingsMark(s.Missions).ToLowerInvariant()}) se sustituye por una nueva.";

        /// <summary>A campaign from before the 0.7 kept aside (setAsideNote).</summary>
        public static string SetAside(string file)
        {
            // The campaign from before the expeditions (Red, 16/11: save.antes-de-las-runs.json; Lore's words).
            if ((file ?? "").Contains("antes-de-las-runs")) return $"La campaña anterior no se puede continuar con esta versión. Queda guardada aparte, en {file}. Con «Nueva campaña» empezáis otra.";
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
            ("elimination", "Limpieza", "Abatid a todo lo que haya en el mapa."),
            ("recovery", "Botín", "Recuperad la carga y salid " + Lore.Bus.In + "."),
            ("sabotage", "Sabotaje", "Apagad la alarma antes de que acabe la cuenta atrás."),
            ("hack", "Arreglar la radio", "Arreglad la radio. Si falla, salta la alarma."),
            // Not «el VIP»: neither an English acronym nor always a man (Lore, 23/10).
            ("rescue", "Rescate", "Llegad hasta la persona retenida y sacadla con vida."),
            ("scavenge", "Saqueo", "Registrad las casas y salid con lo que podáis: cada ruido trae más muertos."),
        };
    }
}
