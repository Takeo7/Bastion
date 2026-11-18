namespace Bastion.UI.Hud
{
    /// <summary>
    /// The HUD's own words, that lived in hud.ts and game.ts (docs/UNITY-UI.md §6: UI keeps them, Lore reviews them).
    /// What lives in lore.ts comes from the engine's Lore instead. As they are in the 0.10.1.
    /// </summary>
    public static class HudText
    {
        /// <summary>
        /// A text with its key (Lore, 7/11: the key in its own field): «Ver (F)» at the desktop, «Ver» with the finger, which
        /// has no keys.
        /// </summary>
        public static string Keyed(string text, string key) => Bastion.Client.GameInput.Touch ? text : $"{text} ({key})";

        /// <summary>A help that is only a key («Esc»): none with the finger.</summary>
        public static string KeyOnly(string key) => Bastion.Client.GameInput.Touch ? null : key;

        public static class Turn
        {
            public const string InPlayTip = "Un soldado cada vez: sigue en juego hasta que no le quede nada, termines su turno o cedas el mando.";
            public const string EnemyLock = "Turno enemigo";

            /// <summary>The bar resting while the partner holds the command: the keys do nothing then (Arturo, 5/11 #15), moves are planned.</summary>
            public static string PartnerLock(string name) => $"Turno de {name}: puedes planear los movimientos";
            public static string InPlay(string name) => $"EN JUEGO · {name.ToUpperInvariant()}";
        }

        /// <summary>«Terminar turno», a key of the bar with no words on it (UI-COMBATE UI-04): what it says is in its help.</summary>
        public static class EndTurn
        {
            public static string Act => Keyed("Terminar turno", "Retroceso");
            public const string Ready = "A punto. Pulsa para cancelar.";
            public static string EnemyTurn => Bastion.Client.GameInput.Touch ? Touch.EnemyTurn : "Turno enemigo. Mantén Espacio para verlo más rápido.";
            public const string Off = "Terminar turno";

            public static string Confirm(string detail) => string.IsNullOrEmpty(detail) ? "¿Terminar? Pulsa otra vez." : $"¿Terminar? {detail}. Pulsa otra vez.";
            public static string Waiting(string name) => $"A punto. Esperando a {name}. Pulsa para cancelar.";
            /// <summary>Ready without the command (Arturo, 7/11): the turn is over when the holder's is.</summary>
            public static string ReadyHolder(string name) => $"A punto. {name} sigue con el mando: el turno acaba cuando termine. Pulsa para cancelar.";
            /// <summary>
            /// With a soldier in play (Arturo, 7/11, Ana's #5: one key): «Terminar el turno de Yuki», what that does, and that
            /// a second press ends one's own turn (Lore: not «everyone's», with a partner whose soldiers go on).
            /// </summary>
            public static string Done(string name, string to) => $"{Keyed(HudText.Done.Key(name), "Retroceso")}. {HudText.Done.Tip(name, to)} Pulsa otra vez para terminar tu turno.";
            public static string Command(string name) => Bastion.Client.GameInput.Touch ? $"Tiene el mando {name}. Puedes planear los movimientos." : $"Tiene el mando {name}. Puedes planear: clic en una casilla.";

            /// <summary>
            /// A soldier in play with something left, «Terminar turno» pressed once (Arturo, 15/11): what it still has, and
            /// that a second press ends its turn; meanwhile its actions, its movement and its card blink (HudRoot.PointOut).
            /// `tiles`, its movement left as HudText.Moves.Tiles says it; null, none.
            /// </summary>
            public static string Left(string name, int actions, string tiles)
            {
                var what = new System.Collections.Generic.List<string>();
                if (actions > 0) what.Add(actions == 1 ? "1 acción" : $"{actions} acciones");
                if (tiles != null) what.Add(tiles == "1" ? "1 casilla de movimiento" : $"{tiles} casillas de movimiento");
                var one = what.Count == 1 && (actions == 1 || tiles == "1");
                return $"A {name} {(one ? "le queda" : "le quedan")} {string.Join(" y ", what)}. Pulsa otra vez para terminar su turno.";
            }
        }

        /// <summary>The cover's shield on the soldier's panel (Arturo, 7/11; Lore's words): its title and what it says.</summary>
        public static class CoverShield
        {
            public const string Title = "Cobertura";
            public static string Tip(string name) => $"La cobertura de {name} frente a los enemigos a la vista. Azul: completa. Amarillo: media. Rojo: le falta cobertura ante alguno. Sin enemigos a la vista, el rojo no sale.";
        }

        /// <summary>
        /// A soldier's turn ended by hand (Arturo, 7/11; DoneCommand; Lore's words, TEXTOS-0124.md): «Terminar turno» while
        /// it is in play (EndTurn.Done; its own key on the bar went, Ana's #5) and the question on the call's card when
        /// another is picked with one in play.
        /// </summary>
        public static class Done
        {
            public static string Key(string name) => $"Terminar el turno de {name}";
            /// <summary>`to`: who the command passes to; null, it doesn't.</summary>
            public static string Tip(string name, string to) => $"{name} no hace nada más este turno: pierde las acciones y el movimiento que le queden.{(to != null ? $" El mando pasa a {to}." : "")}";
            /// <summary>«le quedan 3 puntos de movimiento», «le queda 1 acción», «le quedan 2 acciones y 5 puntos de movimiento».</summary>
            static string Left(int ap, int moves)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (ap > 0) parts.Add(ap == 1 ? "1 acción" : $"{ap} acciones");
                var points = Moves.Tiles(moves);
                if (moves > 0) parts.Add(points == "1" ? "1 punto de movimiento" : $"{points} puntos de movimiento");
                return (parts.Count == 1 && (ap == 1 && moves == 0 || ap == 0 && points == "1") ? "le queda " : "le quedan ") + string.Join(" y ", parts);
            }
            public static string State(string name, int ap, int moves) => ap > 0 ? $"{name} aún puede actuar: {Left(ap, moves)}." : $"{name} aún puede moverse: {Left(0, moves)}.";
            public static string Stakes(string to) => to != null ? $"Si terminas su turno, pierde lo que le queda y el mando pasa a {to}." : "Si terminas su turno, pierde lo que le queda.";
            public const string Yes = "Terminar su turno";
            public static string No(string name) => $"Seguir con {name}";
        }

        /// <summary>The command and the camera, in the game's menu now (UI-COMBATE UI-07): they were keys over «Terminar turno».</summary>
        public static class Command
        {
            public const string Pass = "Ceder el mando";
            public const string PassKey = "C";
            public const string GoKey = "F";
            public const string PassTip = "El otro jugador da las órdenes hasta que te lo devuelva";
            public const string WatchTip = "Lleva la cámara a lo que está haciendo el otro jugador";
            public const string GoTip = "Lleva la cámara al soldado al que le toca y lo elige";

            public static string Watch(string name) => $"Ver a {name}";
            public static string GoTo(string name) => $"Ir a {name.Split(' ')[0]}";
        }

        public static class Players
        {
            // Without gender (Lore, 26/10): «A punto», «Sin conexión».
            public const string Ready = "A punto";
            public const string WithActions = "Con acciones";
            /// <summary>Beside the name of who holds the command (Arturo, 7/11).</summary>
            public const string Command = "MANDO";
            /// <summary>Movement left but no actions (Arturo, 5/11 #14): the command may still go to them.</summary>
            public const string WithMoves = "Con movimiento";
            // Beside «Con movimiento», neither (Lore, 6/11).
            public const string NoActions = "Sin acciones ni movimiento";
            public const string Offline = "Sin conexión";
            public const string Away = "Ausente";
        }

        public static class Shot
        {
            public const string Impact = "IMPACTO";
            public const string Crit = "CRÍTICO";
            public const string Cancel = "Cancelar";
            public static string FireTip => KeyOnly("Espacio o Intro");
            public static string CancelTip => KeyOnly("Esc");
            public static string PrevTip => Keyed("Objetivo anterior", "Mayús+Tab");
            public static string NextTip => Keyed("Siguiente objetivo", "Tab");

            /// <summary>Which of how many targets, and how to change (Arturo, 5/11 #11); with the finger, the arrows.</summary>
            public static string Index(int n, int of) => Bastion.Client.GameInput.Touch ? $"{n}/{of}" : $"{n}/{of} · Tab";
            public const string LockedTip = "Sin el mando solo se puede apuntar: el porcentaje sirve para planear";
            public const string SelfLocked = "Sin el mando solo puedes consultarla";
            public static string SelfHint => Bastion.Client.GameInput.Touch ? "Pulsa otra vez para usarla" : "Otra vez, Espacio o Intro: usar · Esc: cancelar";
            public const string NoiseTip = "Todo enemigo a esa distancia lo oye y va a mirar";

            public static string Graze(int odds, string damage) => string.IsNullOrEmpty(damage) ? $"Rozadura posible: {odds}%" : $"Rozadura posible: {odds}% (daño {damage})";
            public static string Damage(string hit, string crit) => string.IsNullOrEmpty(crit) ? $"DAÑO {hit}" : $"DAÑO {hit} · crít. {crit}";
        }

        public static class Mission
        {
            // The expeditions' stages (H5, H6; Lore to confirm).
            /// <summary>`full`: come, with no seat left and some of the squad still out (Red's pass, 18/11), as the Rescate says.</summary>
            public static string Hold(int until, int left, bool full = false) => left > 0 ? $"Aguantad: La Lata llega en el turno {until}, dentro de {left} {(left == 1 ? "turno" : "turnos")}" : full ? Bastion.Engine.Lore.BoardText.FullObjective : "La Lata ha llegado: subid";
            public const string HoldTip = "Llegan oleadas cada dos turnos, cada vez mayores. Hasta que llegue La Lata no se puede salir.";
            /// <summary>«Matad al Toro…»: «a el» contracted.</summary>
            public static string Boss(string name) => $"Matad {(name.StartsWith("El ") ? "al " + name.Substring(3) : "a " + name)} y salid en La Lata";
            public static string BossDead(string name) => $"{name} ha caído: salid en La Lata";
            /// <summary>
            /// A Guarida and a Travesía (H5): Saqueos underneath, with an objective of their own (the pass of 16/11: they
            /// said the Saqueo's «Registrad lo que podáis»; Lore to confirm).
            /// </summary>
            public const string Lair = "Abrid la caja de la Guarida y salid en La Lata";
            public const string LairOpen = "Caja abierta: salid en La Lata";
            public const string Crossing = "Cruzad el mapa: La Lata espera en el otro extremo";
            public const string IntroObjective = "Objetivo: ";
            public const string IntroClose = "Entendido";
            public const string DockTip = "Ver la misión";
            public const string Victory = "MISIÓN CUMPLIDA";
            public const string Defeat = "MISIÓN FRACASADA";
            /// <summary>
            /// A stage's end on an expedition (Lore, L5, 17/11), as its report says it (StrategyText.Runs.Ended): who comes
            /// home (won, the living; lost, those out in La Lata) gives the title; the button goes on with the expedition, or
            /// back to the base when it ends (nobody home, or the boss's stage).
            /// </summary>
            public const string StageWon = "ETAPA SUPERADA";
            public const string ChapterWon = "CAPÍTULO SUPERADO";
            public const string StageShort = "SIN LA RECOMPENSA";
            public static string BossStands(string boss) => $"{boss.ToUpperInvariant()} SIGUE EN PIE";
            public const string SquadLost = "ESCUADRA PERDIDA";
            public const string SquadLostLine = "Lo que llevaban se pierde, y la expedición acaba.";
            public const string StageAbandoned = "Etapa abandonada";
            /// <summary>What a stage that is a Saqueo underneath brings home: found there and aboard La Lata.</summary>
            public const string Haul = "Botín a salvo: ";
            public const string NoHaul = "No habéis encontrado nada";
            public static string HaulMore(int n) => $"{n} más";
            public const string RunToBase = "VOLVER A LA BASE";
            public const string RunReturning = "VOLVIENDO A LA BASE…";
            public const string RunGoOn = "SEGUIR LA EXPEDICIÓN";
            public const string RunGoingOn = "SIGUIENDO LA EXPEDICIÓN…";
            public const string ToBase = "VOLVER A LA BASE";
            public const string Returning = "VOLVIENDO AL BASTIÓN…";
            public const string Again = "OTRA IGUAL (MAPA NUEVO)";
            public const string AgainSiege = "OTRO ASEDIO";
            public const string Random = "MISIÓN AL AZAR";
            public const string ToMenu = "VOLVER AL MENÚ";
            public const string ToMenuTip = "Los dos volvéis al menú principal";
            public const string Horde = "RUIDO";
            /// <summary>The noise meter's help: only the squad's noise fills it now, in every mission (Arturo, 02/11 A4).</summary>
            public const string NoiseTip = "Solo el ruido que haces atrae a la horda. A 90 dB se oye llegar una oleada, que entra por los bordes un turno después.";

            public static string Decibels(int db) => $"{db} dB";

            public static string Turns(int n) => n == 1 ? "1 TURNO" : $"{n} TURNOS";
        }

        public static class Banner
        {
            public static string Go => Bastion.Client.GameInput.Touch ? "Toca para seguir" : "Clic para seguir";
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

            // The pause (Arturo, 28/10; Lore's words): under «MENÚ» for whoever opened it, alone or with a partner.
            public const string PausedSolo = "Partida en pausa.";
            public const string PausedBoth = "Partida en pausa para los dos.";
            /// <summary>Over the battle, for the other player.</summary>
            public static string PartnerPaused(string partner) => $"Partida en pausa: {partner} está en el menú.";
            public const string PartnerPausedSub = "Sigue cuando lo cierre.";
            /// <summary>An order tried while it waits (the host's own rejection says the same).</summary>
            public const string PausedOrder = "La partida está en pausa: la orden no se ha hecho.";
        }

        public static class Notes
        {
            public static string See => Keyed("Ver", "F");
        }

        public static class Log
        {
            /// <summary>After an arrow icon (▴ and ▾ aren't in Barlow).</summary>
            public const string Fold = "Plegar";

            public static string More(int n) => $"{n} más";

            /// <summary>At the end of a line of the partner's: the camera there.</summary>
            public const string See = "Ver";
        }

        public static class Compass
        {
            public const string About = "Norte, este, sur y oeste del mapa.";

            public static string Next(string sides) => $"La próxima oleada entra por {sides}.";
            public static readonly string[] Letters = { "N", "E", "S", "O" };
            public static readonly string[] Sides = { "el norte", "el este", "el sur", "el oeste" };
        }

        /// <summary>
        /// The weather (GameState.weather, Campaign.OfferWeather; Arturo, 01/11): its name, and what it changes, from the
        /// engine's figures (Content.Weather), for the mission's card and the objective's in the fight.
        /// </summary>
        public static class Weather
        {
            public static readonly System.Collections.Generic.Dictionary<string, string> Names = new System.Collections.Generic.Dictionary<string, string>
            {
                [Bastion.Engine.Weathers.Drizzle] = "Llovizna",
                [Bastion.Engine.Weathers.Downpour] = "Aguacero",
                [Bastion.Engine.Weathers.Storm] = "Tormenta",
                [Bastion.Engine.Weathers.Snowfall] = "Nevada",
                [Bastion.Engine.Weathers.Blizzard] = "Ventisca",
            };

            /// <summary>Its name; null for a clear day (or one this version doesn't know).</summary>
            public static string Name(string weather) => weather != null && Names.TryGetValue(weather, out var name) ? name : null;

            /// <summary>«Tormenta: se ve menos lejos y los ruidos se oyen menos.»</summary>
            public static string About(string weather)
            {
                var name = Name(weather);
                if (name == null) return null;
                var sight = Bastion.Engine.Content.Weather.SightOf(weather) < 1;
                var noise = Bastion.Engine.Content.Weather.NoiseOf(weather) < 1;
                var what = sight && noise ? "se ve menos lejos y los ruidos se oyen menos" : sight ? "se ve menos lejos" : noise ? "los ruidos se oyen menos" : "no cambia lo que se ve ni lo que se oye";
                return $"{name}: {what}.";
            }
        }

        public static class Skills
        {
            public const string None = "Aún no ha gastado puntos en el tablero";

            public static string Best(string name) => $"Más puntos en el tablero: {name}";
            public static string Tied(string names) => $"Empatan en el tablero: {names}";
        }

        /// <summary>The map from above: the camera straight down over all of it (Arturo, 31/10 #12 and 01/11).</summary>
        public static class MiniMap
        {
            public const string Title = "MAPA";
            public static string Hint => Bastion.Client.GameInput.Touch ? "Toca un sitio para bajar la cámara allí" : "Clic: bajar la cámara allí · M o Esc: volver";
        }

        /// <summary>The movement left, apart from the actions (Arturo, 5/11 #14), in points (Lore's: a tile one; nobody walks a diagonal since 7/11, Arturo): a path's cost is two a point.</summary>
        /// <summary>
        /// The finger's words (docs/IOS.md, phase 2; Lore's .dev-snapshots/lore/TEXTOS-TACTILES.md, 7/11): what a click or a
        /// key said, said with the finger. «Pulsa» stays for buttons; «toca», a place on the screen.
        /// </summary>
        public static class Touch
        {
            /// <summary>Under the destination after the first tap.</summary>
            public const string AgainMove = "Toca otra vez para mover.";
            /// <summary>The same, planning without the command.</summary>
            public const string AgainPlan = "Toca otra vez para planear el camino.";
            /// <summary>The first tap where the squad would be seen: the second moves.</summary>
            public const string Concealed = "Desde ahí os ven: se rompe la ocultación. Toca otra vez para mover.";
            public const string Door = "Puerta · toca otra vez: abrir (no gasta acciones)";
            public static string Boards(string what) => $"{what} · toca otra vez: quitar las tablas";
            public static string Open(string name, string verb) => $"{name} · toca otra vez: {verb}";
            /// <summary>With something in hand: its key again or its place touched (cancelling is «Cancelar»'s; another, the panel's arrows).</summary>
            public static string Hand(string what, string verb) => $"Pulsa otra vez o toca {what}: {verb}";
            public const string EnemyTurn = "Turno enemigo. Mantén pulsado » para verlo más rápido.";
            public const string EnemySpeed = " En el turno enemigo, mantén pulsado » para verlo más rápido.";
            public const string ThrowWhere = "Toca dónde lanzarla.";
            public static string Planned(string name, string sees) => $"Plan para {name}: lo ve {sees}. Toca otra vez esa casilla para quitarlo.";
            /// <summary>The compass, with the finger in every mission (Arturo, 7/11: turning by steps, two fingers or the compass).</summary>
            public const string Compass = "Norte, este, sur y oeste del mapa. Toca su mitad izquierda o derecha para girar la cámara.";
        }

        /// <summary>The finger's buttons (TouchButtons; Lore, TEXTOS-TACTILES.md §4: they go by their labels).</summary>
        public static class TouchButtons
        {
            public const string Menu = "Abrir el menú y pausar la partida";
            public const string Map = "Mapa";
            public const string FloorUp = "Subir de planta";
            public const string FloorDown = "Bajar de planta";
            public const string Hurry = "Mantenlo pulsado: el turno enemigo va más rápido";
            public const string Pass = "Ceder";
            public const string PassTip = "Ceder el mando";
        }

        /// <summary>
        /// A zombie finding the squad (Arturo, 15/11: «que se note la detección»; Lore's words): over the soldier it saw (for
        /// anyone, a man or a woman), and, after who saw whom, that the whole squad is found, not that one alone.
        /// </summary>
        public static class Spotted
        {
            public const string Seen = "¡A LA VISTA!";
            public const string All = "Toda la escuadra queda al descubierto.";
        }

        /// <summary>Golpe silencioso (Gameplay, 16/11; Lore's words).</summary>
        public static class Takedown
        {
            public const string Name = "Golpe silencioso";
            /// <summary>Over each of the dead the picked one can take down now.</summary>
            public const string Tag = "GOLPE SILENCIOSO";
            /// <summary>Over the one aimed at, or pointed at beside it.</summary>
            public const string Kills = "Lo mata sin ruido";
            /// <summary>The aim panel's: what it does.</summary>
            public const string Effect = "Lo mata de un golpe y sin ruido. Su grupo no se entera. No rompe la ocultación.";
            /// <summary>The pointer on one beside: «Golpe silencioso · Lo mata sin ruido», «Golpe silencioso · Ya está alerta» (Lore: a «·», not a second «:»).</summary>
            public static string On(string line) => $"{Name} · {line}";
        }

        /// <summary>Noises (EventPlayer.PlayNoise).</summary>
        public static class Noise
        {
            /// <summary>Over each of the dead in sight that heard one, after the noise's loudspeaker (Gameplay, 16/11; Lore: they heard something, but haven't seen you).</summary>
            public const string Heard = "?";
        }

        /// <summary>Throwing (CombatController.ThrowNote).</summary>
        public static class Throw
        {
            /// <summary>Pointed past its reach: it falls where the line is, nearer (Arturo, 15/11, E3; Lore's words).</summary>
            public const string Short = "No llega más lejos";
        }

        public static class Moves
        {
            /// <summary>«5», «6,5» (half a point: a trap's wire costs a tile and a half, Content.Traps.Wire).</summary>
            public static string Tiles(int cost) => cost % 2 == 0 ? (cost / 2).ToString() : $"{cost / 2},5";
            /// <summary>A tile only the hidden squad's extra reaches (Gameplay, 16/11; Lore's words: concealment breaks by an attack too).</summary>
            public const string Hidden = "Con el movimiento extra de la ocultación: si se rompe, se pierde";
            /// <summary>«/8», after the movement left on the bar (Ana, 7/11 #7).</summary>
            public static string Of(int cost) => $"/{Tiles(cost)}";
            /// <summary>`hidden`: the hidden squad's extra in it (Paths.ConcealedBonus, Gameplay 16/11), said after; 0, none.</summary>
            public static string Tip(int left, int full, int hidden = 0) => $"Movimiento: le quedan {Tiles(left)} de {Tiles(full)} puntos. Cada casilla cuesta 1. Moverse no gasta acciones, y el movimiento se reparte en tramos, antes y después de actuar. Gastarlo todo de una vez, en un solo tramo, es correr, y se oye.{(hidden > 0 ? " " + HiddenTip(hidden) : "")}";
            /// <summary>The hidden squad's extra, on the movement's help (Gameplay, 16/11; Lore's words).</summary>
            public static string HiddenTip(int extra) => $"Mientras la escuadra está oculta, tiene {Tiles(extra)} puntos de más. Al romperse la ocultación, pierde {Tiles(extra)} de los que le queden.";
            public const string Stat = "Movimiento";
        }

        /// <summary>
        /// The enemies' sight (Arturo, 5/11 #6, #9): its button, whose help says what the red on the ground is (Ana, 7/11
        /// #10: no legend over it; Lore, 5/11: «notar», as the night's rules and Esconderse say it).
        /// </summary>
        public static class Sight
        {
            public static string Tip => $"{Keyed("Vista de los zombis", "V")}: lo que ve cada uno. El rojo más fuerte, donde notarían a un soldado escondido. En la placa de un zombi, el ojo se abre cuando ve al soldado elegido.";
        }

        /// <summary>The goal's arrow, bottom right (Ana, 7/11 #5: it was taken for a key): only its help.</summary>
        public static class Goal
        {
            public static string Tip => Bastion.Client.GameInput.Touch ? "Hacia el objetivo: la flecha señala dónde está." : "Hacia el objetivo (O): la flecha señala dónde está; la tecla lleva la cámara allí.";
        }

        public static class Squad
        {
            public static string Select => Bastion.Client.GameInput.Touch ? "Un toque: seleccionar · Dos toques: cámara al soldado" : "Seleccionar · Doble clic: cámara al soldado";
            public const string Theirs = "Soldado del otro jugador";
            public const string TheirsCommanded = "Soldado del otro jugador: lo mandas tú mientras no está";
            /// <summary>A partner's soldier's card (Ana, 7/11 #12): whose, and that a click takes the camera there.</summary>
            public static string TheirsLook(string name) => $"{(string.IsNullOrEmpty(name) ? Theirs : $"Soldado de {name}")} · {(Bastion.Client.GameInput.Touch ? "Un toque" : "Clic")}: cámara al soldado";
            public const string Automaton = "Va sola: nadie le da órdenes.";
            public static string SheetTip => " · " + Keyed("La hoja del soldado: tablero, estadísticas y lo que lleva", "H");
            public const string InPlay = "EN JUEGO";
            public const string InPlayTip = "En juego: termina sus acciones antes que otro";

            /// <summary>«DE ANA»; a partner not in the room, «DEL OTRO JUGADOR».</summary>
            public static string Whose(string name) => string.IsNullOrEmpty(name) ? "DEL OTRO JUGADOR" : $"DE {name.ToUpperInvariant()}";
        }

        /// <summary>The windows over the battle: the soldier's sheet (H) and an enemy's file (right click).</summary>
        public static class Window
        {
            public const string SheetKicker = "HOJA DEL SOLDADO";
            /// <summary>Over an enemy's file: the Bastión's (Lore, 16/11: no story in the runs, so not the Atalaya).</summary>
            public const string FileKicker = "FICHA DEL BASTIÓN";
            public const string Carries = "Lleva";
            public const string Skills = "Habilidades";
            public const string Close = "CERRAR";

            public static string Whose(string name) => $" DE {name.ToUpperInvariant()}";
        }

        /// <summary>The backpack on the soldier's panel (hud.ts backpack, packMenuEl, packOffer; docs/FASE8.md §C).</summary>
        /// <summary>A chapter's boss on its bar (H6; Lore's review, 16/11). Its file is Lore.EnemyFiles'.</summary>
        public static class Boss
        {
            public static string Armor(int n) => $"BLINDAJE {n}";
            public const string ChargeReady = "EMBESTIDA LISTA";
            public static string ChargeIn(int turns) => turns == 1 ? "EMBESTIDA EN 1 TURNO" : $"EMBESTIDA EN {turns} TURNOS";
            public const string Tip = "Embiste en línea recta hasta 6 casillas: hace 4 de daño y tumba a quien alcanza, que se queda en el suelo hasta vuestro turno y pierde una acción al levantarse. Si no puede embestir, golpea con los puños: 6 de daño. Contra él no vale el golpe silencioso.";
            /// <summary>A move's destination in his line, with the charge ready at his next turn (the line drawn to it).</summary>
            public const string InLine = "En la línea de la embestida";
        }

        /// <summary>What a run's search found (H3; Lore to confirm).</summary>
        public static class Found
        {
            public const string Nothing = "Nada";

            /// <summary>
            /// Lore's (17/11), each thing with its indefinite article (Lore.GearA): «Anja encuentra una granada y 117 §.»; «Anja
            /// encuentra 117 §. También hay un fusil de cerrojo, pero no le cabe en la mochila: queda en el suelo.»; «Anja
            /// encuentra un fusil de cerrojo, pero no le cabe en la mochila: queda en el suelo.» `many`: what is left on the
            /// floor is more than one thing, or a pile of more than one («no le caben», «quedan»).
            /// </summary>
            public static string Line(string who, System.Collections.Generic.IList<string> into, System.Collections.Generic.IList<string> left, bool many = false)
            {
                if (into.Count == 0 && left.Count == 0) return $"{who} no encuentra nada.";
                var fits = many ? "no le caben en la mochila: quedan en el suelo" : "no le cabe en la mochila: queda en el suelo";
                if (into.Count == 0) return $"{who} encuentra {Ui.List(left)}, pero {fits}.";
                var line = $"{who} encuentra {Ui.List(into)}.";
                if (left.Count > 0) line += $" También hay {Ui.List(left)}, pero {fits}.";
                return line;
            }
        }

        public static class Pack
        {
            /// <summary>The backpack's mark shows and hides its slots (Ana, 7/11 #13; Lore: «huecos», as «Hueco libre»).</summary>
            public static string Pockets => Bastion.Client.GameInput.Touch ? "Un toque: mostrar u ocultar los huecos" : "Clic: mostrar u ocultar los huecos";
            public const string Floor = "EN EL SUELO";
            // The runs' pack in combat (H3; Lore to confirm).
            public const string KitHead = "EQUIPO";
            public const string PackHead = "MOCHILA";
            public const string GiveHead = "DAR A";
            public const string NotThere = "Ahí no va.";
            public const string UseSelf = "Usar";
            public static string HealOther(string name) => $"Curar a {name}";
            public const string DropIt = "Soltar";
            public static string GiveTo(string name) => $"Dar a {name}";
            public const string Squad = "ESCUADRA";
            public const string Nothing = "—";
            public const string NothingTip = "La escuadra aún no lleva nada.";
            public const string Free = "Hueco libre";
            public const string Close = "Cerrar";
            public const string Take = "Coger";
            public const string Swap = "Cambiar";
            public const string OneAction = "1 acción";
            public const string Gratis = "gratis";
            /// <summary>A medicine with Manos de enfermero: no action spent, the medicine yes (Lore: not «gratis»).</summary>
            public const string NoAction = "sin acción";
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
