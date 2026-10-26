namespace Bastion.UI.Hud
{
    /// <summary>
    /// The HUD's own words, that lived in hud.ts and game.ts (docs/UNITY-UI.md §6: UI keeps them, Lore reviews them).
    /// What lives in lore.ts comes from the engine's Lore instead. As they are in the 0.10.1.
    /// </summary>
    public static class HudText
    {
        public static class Turn
        {
            public const string Mine = "TU TURNO";
            public const string Enemy = "TURNO ENEMIGO";
            public const string Over = "FIN DE LA MISIÓN";
            public const string Concealed = "OCULTOS";
            public const string Detected = "DETECTADOS";
            public const string InPlayTip = "Un soldado cada vez: este termina sus acciones antes que otro, o se cede el turno";
            public const string EnemyLock = "Turno enemigo";

            public static string Partner(string name) => $"TURNO DE {name.ToUpperInvariant()}";
            public static string PartnerLock(string name) => $"Turno de {name}: puedes apuntar y planear";
            public static string InPlay(string name) => $"EN JUEGO · {name.ToUpperInvariant()}";
            public static string Number(int turn) => $"Turno {turn}";
        }

        public static class EndTurn
        {
            public const string Act = "TERMINAR TURNO";
            public const string ActKey = "Retroceso";
            public const string Confirm = "¿TERMINAR?";
            public const string ConfirmAgain = "Pulsa otra vez";
            /// <summary>With the check sign after it (an icon: ✓ isn't in Oswald).</summary>
            public const string Ready = "A PUNTO";
            public const string ReadyCancel = "Pulsa para cancelar";
            public const string EnemyTurn = "TURNO ENEMIGO";
            public const string EnemyFaster = "Espacio: más rápido";
            public const string Plan = "Puedes planear: clic en una casilla";

            public static string Waiting(string name) => $"Esperando a {name}";
            public static string Command(string name) => $"MANDO: {name.ToUpperInvariant()}";
            public static string WithActions(int n) => n == 1 ? "1 soldado con acciones" : $"{n} soldados con acciones";
        }

        public static class Command
        {
            public const string Pass = "CEDER EL MANDO";
            public const string PassKey = "C";
            public const string PassTip = "El otro jugador da las órdenes hasta que te lo devuelva (C)";
            public const string WatchTip = "Lleva la cámara a lo que está haciendo el otro jugador (F)";
            public const string GoTip = "Lleva la cámara al soldado al que le toca y lo elige (F)";
            public const string On = " Activada.";
            public const string Off = " Desactivada.";

            public static string Watch(string name) => $"VER A {name.ToUpperInvariant()}";
            public static string GoTo(string name) => $"IR A {name.Split(' ')[0].ToUpperInvariant()}";
        }

        public static class Players
        {
            // Without gender (Lore, 26/10): «A punto», «Sin conexión».
            public const string Ready = "A punto";
            public const string WithActions = "Con acciones";
            public const string NoActions = "Sin acciones";
            public const string Offline = "Sin conexión";
            public const string Away = "Ausente";
        }

        public static class Shot
        {
            public const string Impact = "IMPACTO";
            public const string Crit = "CRÍTICO";
            public const string Cancel = "Cancelar";
            public const string FireTip = "Espacio o Intro";
            public const string CancelTip = "Esc";
            public const string PrevTip = "Objetivo anterior (Mayús+Tab)";
            public const string NextTip = "Siguiente objetivo (Tab)";
            public const string LockedTip = "Sin el mando solo se puede apuntar: el porcentaje sirve para planear";
            public const string SelfLocked = "Sin el mando solo puedes consultarla";
            public const string SelfHint = "Otra vez, Espacio o Intro: usar · Esc: cancelar";
            public const string NoiseTip = "Todo enemigo a esa distancia lo oye y va a mirar";

            public static string Graze(int odds, string damage) => string.IsNullOrEmpty(damage) ? $"Rozadura posible: {odds}%" : $"Rozadura posible: {odds}% (daño {damage})";
            public static string Damage(string hit, string crit) => string.IsNullOrEmpty(crit) ? $"DAÑO {hit}" : $"DAÑO {hit} · crít. {crit}";
        }

        public static class Mission
        {
            public const string IntroObjective = "Objetivo: ";
            public const string IntroClose = "Entendido";
            public const string DockTip = "Ver la misión";
            public const string Victory = "MISIÓN CUMPLIDA";
            public const string Defeat = "MISIÓN FRACASADA";
            public const string ToBase = "VOLVER A LA BASE";
            public const string Returning = "VOLVIENDO AL BASTIÓN…";
            public const string Again = "OTRA IGUAL (MAPA NUEVO)";
            public const string AgainSiege = "OTRO ASEDIO";
            public const string Random = "MISIÓN AL AZAR";
            public const string ToMenu = "VOLVER AL MENÚ";
            public const string ToMenuTip = "Los dos volvéis al menú principal";
            public const string Horde = "HORDA";

            public static string Turns(int n) => n == 1 ? "1 TURNO" : $"{n} TURNOS";
        }

        public static class Banner
        {
            public const string Go = "Clic para seguir";
        }

        /// <summary>The network's line at the top (hud.ts setConnection); «Volviendo a la partida…» is Lore's RejoinLine.</summary>
        public static class Net
        {
            public const string Connecting = "Conectando…";
            public const string Lost = "Conexión perdida · reconectando…";
        }

        /// <summary>The game's menu (hud.ts renderMenu, confirmLeave); abandoning and quitting are in Lore's words (the view's).</summary>
        public static class Menu
        {
            public const string Label = "Menú";
            public const string Tip = "Menú (Esc)";
            public const string Title = "MENÚ";
            public const string Continue = "Continuar";
            public const string Settings = "Ajustes";
            public const string Controls = "Controles";
            // «Menú principal», as everywhere else (Lore, 24/10): «inicio» read as the start screen, which is another.
            public const string Leave = "Volver al menú principal";
            public const string Back = "‹ Volver";
            /// <summary>«Abandonar la misión» off while one's own call waits: the partner answers on their bar.</summary>
            public const string AbandonWaitingTip = "Contesta en su barra";
            public const string LeaveTitle = "¿Volver al menú principal?";
            public const string LeaveCampaign = "La misión queda guardada tal como está; «Continuar campaña» vuelve a ella.";
            public const string LeaveSkirmish = "La escaramuza se acaba.";
            public const string LeaveYes = "Volver al menú";
            public const string LeaveNo = "Seguir aquí";

            public static string LeaveToo(string partner) => $"{partner} vuelve también al menú.";
            public static string AbandonAsks(string partner) => $"{partner} tiene que aceptarlo.";
            public static string AbandonTip(string partner, bool paired) => partner != null ? $"Se lo pregunta a {partner}" : paired ? "Termina la misión para los dos" : "Termina la misión";
        }

        public static class Notes
        {
            public const string See = "Ver (F)";
        }

        public static class Log
        {
            /// <summary>After an arrow icon (▴ and ▾ aren't in Barlow).</summary>
            public const string Fold = "Plegar";

            public static string More(int n) => $"{n} más";
        }

        public static class Compass
        {
            public const string About = "Norte, este, sur y oeste del mapa.";

            public static string Next(string sides) => $"La próxima oleada entra por {sides}.";
            public static readonly string[] Letters = { "N", "E", "S", "O" };
            public static readonly string[] Sides = { "el norte", "el este", "el sur", "el oeste" };
        }

        public static class Floor
        {
            public const string Ground = "Planta baja";
            public const string Down = "Bajar de planta (AvPág)";
            public const string Up = "Subir de planta (RePág)";
            public const string Tip = "RePág / AvPág, o Mayús y la rueda";

            public static string Of(int level, int max) => level == 0 ? Ground : $"Planta {level} de {max}";
        }

        public static class Skills
        {
            public const string None = "Aún no ha practicado ninguna habilidad";

            public static string Best(string name) => $"Su mejor habilidad: {name}";
            public static string Tied(string names) => $"Empatan sus mejores habilidades: {names}";
        }

        public static class Squad
        {
            public const string Select = "Seleccionar";
            public const string Theirs = "Soldado del otro jugador";
            public const string TheirsCommanded = "Soldado del otro jugador: lo mandas tú mientras no está";
            public const string Automaton = "Va sola: nadie le da órdenes.";
            public const string SheetTip = " · La hoja del soldado: tablero, estadísticas y lo que lleva (H)";
            public const string InPlay = "EN JUEGO";
            public const string InPlayTip = "En juego: termina sus acciones antes que otro";

            /// <summary>«DE ANA»; a partner not in the room, «DEL OTRO JUGADOR».</summary>
            public static string Whose(string name) => string.IsNullOrEmpty(name) ? "DEL OTRO JUGADOR" : $"DE {name.ToUpperInvariant()}";
        }

        /// <summary>The windows over the battle: the soldier's sheet (H) and an enemy's file (right click).</summary>
        public static class Window
        {
            public const string SheetKicker = "HOJA DEL SOLDADO";
            public const string FileKicker = "FICHA DE ATALAYA";
            public const string Carries = "Lleva";
            public const string Skills = "Habilidades";
            public const string Close = "CERRAR";

            public static string Whose(string name) => $" DE {name.ToUpperInvariant()}";
        }

        /// <summary>The backpack on the soldier's panel (hud.ts backpack, packMenuEl, packOffer; docs/FASE8.md §C).</summary>
        public static class Pack
        {
            public const string Floor = "EN EL SUELO";
            public const string Squad = "ESCUADRA";
            public const string Nothing = "—";
            public const string NothingTip = "La escuadra aún no lleva nada.";
            public const string Free = "Hueco libre";
            public const string Close = "Cerrar";
            public const string Take = "Coger";
            public const string Swap = "Cambiar";
            public const string OneAction = "1 acción";
            public const string Gratis = "gratis";
            public const string Equip = "Equipar";
            public const string PutOn = "Ponerse";
            public const string Use = "Usar";
            public const string Drop = "Soltar";
            public const string VestOn = "Ya lleva un chaleco puesto.";
            public const string GearOn = "Ya lleva eso puesto.";
            public const string FullFallen = "La mochila está llena: deja algo antes";
            // Why not, in the engine's words (Rules.BackpackRefusal).
            public const string NoWoundsSelf = "No tienes heridas.";
            public const string NoWounds = "No tiene heridas.";
            public const string NoRoom = "No le cabe: lleva la mochila llena.";

            public static string Locked(string holder) => $"Tiene el mando {holder}";
            public static string Tip(int carried, int size) => $"Mochila {carried}/{size}. Cuenta si vuelve: lo que lleva un caído se pierde.";
            public static string Heal(string name) => $"Curar a {name}";
            public static string Give(string name) => $"Dar a {name}";
            public static string Leave(string name) => $"Dejar {name}";
            public static string SwapTitle(string name) => $"Coger {name}: ¿qué dejas?";
            public static string FloorTip(string label, bool full) => full ? $"{label}: la mochila está llena; elige qué dejar" : $"{label}: recogerlo (gratis)";
            public static string FallenTip(int fits, int all) => $"Coge lo que quepa: {fits} de {all}";
            public static string Supplies(int n) => $"Suministros: +{n} §";
            public static string Medicines(int n) => n == 1 ? "Medicinas" : $"Medicinas ×{n}";
            public static string Food(int n) => $"Raciones: +{n}";
            public static string Parts(int n) => $"Piezas: +{n}";
            public static string Weapon(string name) => $"{name}: a la Armería, para un soldado";
        }
    }
}
