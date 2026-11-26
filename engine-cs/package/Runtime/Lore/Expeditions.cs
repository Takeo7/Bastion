using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ----------------------------------------------------------- expeditions

        /// <summary>
        /// The words of the runs (Arturo's box, 16/11): a run is an «Expedición», a node an «Etapa», and «Capítulo».
        /// .dev-snapshots/lore/runs/NOMBRES.md has the reasons.
        /// </summary>
        public static class ExpeditionText
        {
            public const string Name = "Expedición";
            public const string Stage = "Etapa";
            public const string Chapter = "Capítulo";
            /// <summary>
            /// The button that starts one (StartRunCommand), and the base's tab of it: «Expedición» is long for a button.
            /// «Partir», not «Salir» (Arturo, 18/11, T8: it looked like quitting the game).
            /// </summary>
            public const string Start = "Partir";
            public static string StartHelp(int chapter) => $"Parte la escuadra: empieza una expedición del capítulo {chapter}.";
            /// <summary>The button that ends one, back to the base.</summary>
            public const string Return = "Volver a la base";
            public const string ReturnHelp = "Acaba la expedición. Todos se curan, y lo que llevan las mochilas se guarda.";
            /// <summary>
            /// On the stage map where «Volver a la base» was (G22, U21; cuadro 72): where one goes back from. The same rule as
            /// RunTexts.EndFromRefuge, said as a fact.
            /// </summary>
            public const string ReturnWhere = "Se vuelve a la base desde un Refugio o al superar el capítulo.";
            /// <summary>ReturnWhere with the Ruta de vuelta (cuadro 165): from a Trade too (RunTexts.EndFromRefugeOrTrade).</summary>
            public const string ReturnWhereRoute = "Se vuelve a la base desde un Refugio o un Trueque, o al superar el capítulo.";
            /// <summary>Over the stage map: «Expedición · Capítulo 1».</summary>
            public static string Header(int chapter) => $"Expedición · Capítulo {chapter}";
            /// <summary>What a fight with an objective gives on top when it is met (Gameplay's «caja de premio»).</summary>
            public const string Reward = "Recompensa";
        }

        /// <summary>Each kind of stage by name (NodeKinds); the boss's is its own (BossName).</summary>
        public static readonly IReadOnlyDictionary<string, string> StageNames = new Dictionary<string, string>
        {
            [NodeKinds.Normal] = "Saqueo",
            [NodeKinds.Elimination] = "Limpieza",
            [NodeKinds.Recovery] = "Recuperación",
            [NodeKinds.Sabotage] = "Sabotaje",
            [NodeKinds.Rescue] = "Rescate",
            [NodeKinds.Hold] = "Asedio",
            [NodeKinds.Crossing] = "Travesía",
            [NodeKinds.Stealth] = "Sigilo",
            [NodeKinds.Elite] = "Guarida",
            [NodeKinds.Key] = "La llave",
            [NodeKinds.Bitten] = "Mordido",
            [NodeKinds.Wounded] = "El herido", // G23.3: the stage, as Arturo and Producto call it (as «Mordido»), not the person
            [NodeKinds.Split] = "Separados", // G23.4: as Arturo calls it (put by Gameplay, 20/11; kept by Lore)
            [NodeKinds.Raiders] = "Saqueadores", // G23.5: as Arturo calls it (put by Gameplay, 20/11, for the engine to start the stage)
            [NodeKinds.Evacuation] = "Evacuación", // G23.6: as Arturo calls it (put by Gameplay, 20/11, for the engine to start the stage)
            [NodeKinds.Charges] = "Las cargas", // G23 (cuadro 105: as Arturo calls it; put by Gameplay, 21/11, for the engine to start the stage)
            [NodeKinds.Refuge] = "Refugio",
            [NodeKinds.Trade] = "Trueque",
            [NodeKinds.Event] = "Suceso",
            [NodeKinds.Boss] = "Jefe",
        };

        /// <summary>A stage's name on the map: its kind's, or for the boss its own (BossName).</summary>
        public static string StageName(string kind, int chapter = 1)
        {
            return kind == NodeKinds.Boss ? BossName(chapter) : StageNames[kind];
        }

        /// <summary>
        /// The boss of a chapter by name (Arturo's box, 16/11: «El Toro», the Brute of Boss Zombies, for the first). The
        /// Cuervos' of the second and the horde of the third have none yet.
        /// </summary>
        public static string BossName(int chapter)
        {
            return chapter == 1 ? "El Toro" : StageNames[NodeKinds.Boss];
        }

        /// <summary>
        /// A boss on the field (H6: El Toro's roared, charged, knockedDown and gotUp). `name`: BossName, or the template's.
        /// A soldier knocked down is «en el suelo», not «tumbado»: it goes over anyone.
        /// </summary>
        public static class BossText
        {
            /// <summary>The banner when it roars: its name, and under it what the roar does.</summary>
            public static string RoarBanner(string name) => name.ToUpperInvariant();
            public const string RoarSub = "Ruge: todos los muertos del mapa van hacia él.";
            public static string RoarLog(string name) => $"{name} os ha visto y ruge. Todos los muertos del mapa van hacia él.";
            /// <summary>A charge that reaches `who`, and one the way cuts (its target null).</summary>
            public static string ChargeLog(string name, string who) => $"{name} embiste a {who}, que cae al suelo.";
            public static string ChargeCutLog(string name) => $"{name} embiste, pero algo le corta el paso.";
            /// <summary>Over a soldier knocked down, and its log line when it gets up.</summary>
            public const string DownTag = "EN EL SUELO";
            public static string GotUpLog(string who) => $"{who} se levanta: le cuesta una acción.";
        }

        /// <summary>
        /// La llave (G23.1; Gameplay's .dev-snapshots/gameplay/g23/LLAVE.md; Arturo, cuadros 71, 73 and 79): the police
        /// station's armoury locked, its key on one of the dead in uniform, its wall to be blown as the other way in.
        /// </summary>
        public static class KeyText
        {
            /// <summary>The objective, a line at a time (UI's strip): the key to find, the armoury to open, La Lata.</summary>
            public const string FindKey = "Buscad la llave: la lleva un muerto de uniforme";
            /// <summary>Dropped by a soldier who fell (or by a body that got up): to be picked up.</summary>
            public const string KeyOnFloor = "La llave está en el suelo: recogedla";
            public const string OpenArmory = "Abrid la armería, o volad su pared";
            public const string Leave = "Salid " + Bus.In;
            /// <summary>The strip's help.</summary>
            public const string ObjectiveTip = "La armería tiene todos sus muebles llenos. La llave la lleva uno de los muertos de uniforme: matadlos y registrad sus cuerpos. Si cae quien la lleva, queda en el suelo. Una granada contra la pared también abre la armería, con ruido.";
            /// <summary>The key in the pack: its sheet's help.</summary>
            public const string Help = "Abre la puerta de la armería. Se gasta al abrirla, y no sale de esta etapa.";
            /// <summary>«Abrir la puerta» on the armoury's without the key (Abilities, OpenDoor); with it, the action's name.</summary>
            public const string Locked = "Está cerrada. Hace falta la llave de la armería.";
            public const string OpenWithKey = "Abrir con la llave";
            /// <summary>When it falls to the floor (its bearer down, or a body that had it getting up): the log, and over the tile.</summary>
            public const string Dropped = "La llave de la armería cae al suelo.";
            public const string DroppedFloat = "LLAVE AL SUELO";
            /// <summary>The armoury opened with the key (UnlockedEvent): the log, and over the door.</summary>
            public static string Unlocked(string who) => $"{who} abre la armería con la llave.";
            public const string UnlockedFloat = "ARMERÍA ABIERTA";
        }

        /// <summary>
        /// Mordido (G23.2; Gameplay's .dev-snapshots/gameplay/g23/MORDIDO.md; Arturo, cuadros 73 and 87): a Rescate whose
        /// survivor is bitten, BiteTurns of the squad's turns from their finding (MissionState.BiteTurns, down at the end of
        /// each), cured by the map's one antiviral. Finding, talking and taking them home are the Rescate's (VipObjective).
        /// </summary>
        public static class BiteText
        {
            /// <summary>The objective's own steps (UI's strip).</summary>
            public const string FindAntiviral = "Buscad el antiviral: está en uno de los armarios o estantes de medicinas";
            public static string GiveAntiviral(string who) => $"Dadle el antiviral a {who}";
            public const string AntiviralOnFloor = "El antiviral está en el suelo: recogedlo";
            public const string TurnedObjective = "Sin recluta ni recompensa: salid " + Bus.In;
            /// <summary>Over their head from their finding: the turns left (1: it ends with this one).</summary>
            public static string Countdown(int turns) => turns <= 1 ? "Se convierte al acabar este turno" : $"Se convierte en {turns} turnos";
            /// <summary>The log's warning with 2 and with 1 turn left.</summary>
            public static string Warn(string who, int turns) => turns <= 1 ? $"{who} se convierte al acabar este turno." : $"A {who} le quedan {turns} turnos para convertirse.";
            /// <summary>Cured (CuredEvent): the log, and over them.</summary>
            public static string Cured(string by, string who) => $"{by} le da el antiviral a {who}. Ya no se convertirá.";
            public const string CuredFloat = "ANTIVIRAL";
            /// <summary>Turned: the log, and over them.</summary>
            public static string Turned(string who) => $"{who} se convierte. Sin recluta ni recompensa: salid {Bus.In}.";
            public const string TurnedFloat = "SE CONVIERTE";
            /// <summary>A medicine used on them, or refused at full health: it heals, not the bite.</summary>
            public const string MedicineNo = "La medicina cura la salud, no la mordedura: hace falta el antiviral.";
            /// <summary>
            /// «Usar» the antiviral on anyone else, and on them once cured (RunPack.Refusal): said of the bite, not of the
            /// person, so nothing has to agree with who they are.
            /// </summary>
            public const string AntiviralOnlyBite = "El antiviral solo sirve contra la mordedura.";
            public const string AlreadyCured = "La mordedura ya está curada.";
            /// <summary>The antiviral in the pack: its sheet's help.</summary>
            public const string AntiviralHelp = "Cura la mordedura: se usa desde una casilla de al lado, con «Usar». Se gasta, y no sale de esta etapa.";
        }

        /// <summary>
        /// El herido (G23.3; Gameplay's .dev-snapshots/gameplay/g23/HERIDO.md; Arturo, cuadros 71, 73 and 92): a Rescate whose
        /// survivor has a broken leg and never acts (Unit.Lame). Carried to La Lata (Cargar, Dejar, Pasar: an action each, from
        /// beside), which won't leave while they are out and alive; joined on the first carry, with no Hablar. Said by their
        /// name or as «quien tiene la pierna rota», never «el herido», so nothing has to agree with who they are.
        /// </summary>
        public static class WoundedText
        {
            /// <summary>The tiles a carrier walks at most a turn (Paths.CarryMoves).</summary>
            private static int Tiles => Paths.CarryMoves / Paths.CostOrtho;

            /// <summary>The objective's steps (UI's strip): to find (the Rescate's own), to carry, to bring, to leave; and if they die.</summary>
            public const string Search = TalkText.Search;
            public static string Carry(string who) => $"Cargad a {who} a hombros";
            public static string Bring(string who) => $"Llevad a {who} {Bus.To}";
            public const string Leave = "Salid " + Bus.In;
            public const string DeadObjective = "Sin recluta ni recompensa: salid " + Bus.In;
            /// <summary>The strip's help.</summary>
            public static string ObjectiveTip => $"No anda: se le lleva a hombros. La primera vez que se le carga, se une a la escuadra. Cargar, dejar y pasar cuestan una acción, siempre desde una casilla de al lado; recibir no cuesta nada. Cargando, se anda como mucho {Tiles} casillas por turno y no se dispara el arma larga. Si cae quien carga, queda en el suelo, y ahí le pueden morder. {Bus.Name} no sale mientras siga fuera con vida.";

            /// <summary>Over the flags: whoever carries them, and them on the floor.</summary>
            public const string CarrierTag = "CARGANDO";
            public const string FloorTag = "PIERNA ROTA";
            /// <summary>The same on the busts, which fit no more than «A BORDO» (UI, 19/11: «CARGAN…», «PIERNA R…»).</summary>
            public const string CarrierTagShort = "CARGA";
            public const string FloorTagShort = "NO ANDA";
            /// <summary>The flag's tag for one on the floor: La Camilla's downed say so (StretcherText.Tag); the busts keep «NO ANDA».</summary>
            public static string FloorTagOf(Unit u) => u.Downed == true ? StretcherText.Tag(u.Appearance?.Sex) : FloorTag;

            /// <summary>
            /// The actions and their help (Content); Dejar's name on a tile of La Lata's, where they go in at once. Also for La
            /// Camilla's downed (Rules.Carriable): «quien no puede andar», not «la pierna rota», and the joining in ObjectiveTip.
            /// </summary>
            public const string CarryAction = "Cargar";
            public static string CarryHelp => $"Carga a hombros a quien no puede andar, desde una casilla de al lado. Cargando, andas como mucho {Tiles} casillas por turno y no disparas el arma larga; la pistola, sí.";
            public const string SetDownAction = "Dejar";
            public const string SetDownHelp = "Deja a quien llevas a hombros en una casilla libre de al lado. En una casilla de " + Bus.Name + ", sube a bordo en el acto.";
            public static string SetDownAboardAction(string who) => $"Subir a {who} {Bus.To}";
            public const string PassAction = "Pasar";
            public const string PassHelp = "Pasa a quien llevas a hombros a alguien de la escuadra que tengas al lado y no cargue a nadie. Recibir no cuesta nada.";

            /// <summary>The refusals (Abilities, Rules): Hablar to them; La Lata out without them (`who` null while nobody has seen them).</summary>
            public const string AlreadyCarrying = "Ya llevas a alguien a hombros.";
            public const string NobodyToCarry = "No hay nadie a quien cargar.";
            public const string NotBeside = "Ponte al lado de quien no puede andar.";
            public const string NotCarrying = "No llevas a nadie.";
            public const string NoFreeTile = "No hay ninguna casilla libre a tu lado.";
            public const string NobodyToPass = "No tienes al lado a nadie de la escuadra que pueda cargar.";
            public const string NoLongArm = "Cargando a alguien no se dispara el arma larga.";
            public const string NoTalk = "No anda: se le carga a hombros, con «Cargar».";
            public static string NotWithout(string? who) => string.IsNullOrEmpty(who) ? "No se sale sin quien pide ayuda en la zona marcada." : $"No se sale sin {who}.";

            /// <summary>
            /// The log: joined (the first carry), carried, passed on, set down on the floor or in La Lata (then not their own
            /// Boarded line too), and fallen with their carrier (with its float over the tile).
            /// </summary>
            public static string Joined(string who) => $"{who} se une a la escuadra.";
            public static string Carried(string by, string who) => $"{by} carga a {who} a hombros.";
            public static string Passed(string from, string to, string who) => $"{from} pasa a {who} a hombros de {to}.";
            public static string SetDown(string by, string who) => $"{by} deja a {who} en el suelo.";
            public static string SetDownAboard(string by, string who) => $"{by} sube a {who} {Bus.To}.";
            public static string Fell(string by, string who) => $"{who} cae al suelo con {by}.";
            public const string FellFloat = "AL SUELO";
        }

        /// <summary>
        /// La Camilla (0.14.11; cuadros 164 and 165; Gameplay's ENTREGA-0.14.11): the squad's first soldier to fall in an
        /// expedition stays on the floor alive (Unit.Downed, DownedEvent), is carried to La Lata with WoundedText's actions,
        /// and comes out of the fight with 1 of health. `sex` is Appearance.Sex: the words agree with them.
        /// </summary>
        public static class StretcherText
        {
            private static bool F(string? sex) => sex == Sexes.F;
            private static string Them(string? sex) => F(sex) ? "la" : "lo";

            /// <summary>«malherido», «malherida».</summary>
            public static string Word(string? sex) => F(sex) ? "malherida" : "malherido";
            /// <summary>Over the flag (WoundedText.FloorTagOf) and over the tile as they fall (DownedEvent).</summary>
            public static string Tag(string? sex) => F(sex) ? "MALHERIDA" : "MALHERIDO";

            /// <summary>The refusals: their own pack (RunPack), and a medicine on them (RunPack, Rules).</summary>
            public static string NoPack(string? sex) => $"Está {Word(sex)}: no puede hacer nada.";
            public static string NoMedicine(string? sex) => $"Está {Word(sex)}: en combate no se cura. Hay que llevar{Them(sex)} {Bus.To}.";

            /// <summary>The combat log as they fall (DownedEvent), and the partner's notice (UI adds «· Ver (F)»).</summary>
            public static string Downed(string who, string? sex) => $"{who} cae {Word(sex)}: hay que llevar{Them(sex)} a hombros {Bus.To}.";
            public static string Fallen(string who, string? sex) => $"{who} ha caído {Word(sex)}";
            /// <summary>The help of their tag, over the figure and on the bust (UI).</summary>
            public static string TagHelp(string? sex) => $"No anda ni actúa. Llevad{Them(sex)} a hombros {Bus.To}: si {Them(sex)} vuelven a herir, o se queda fuera, muere.";

            /// <summary>
            /// The expedition's log: home from the fight (RunFights), and La Camilla spent (RunState.StretcherUsed), however
            /// the downed came out.
            /// </summary>
            public static string Home(string who, string? sex) => $"{who} vuelve {Word(sex)} en la Camilla, con 1 de salud.";
            public const string Spent = "La Camilla ya se ha usado en esta expedición.";
        }

        /// <summary>
        /// Separados (G23.4; Gameplay's .dev-snapshots/gameplay/g23/SEPARADOS.md; Arturo, cuadros 71, 94 and 95): a Saqueo
        /// whose squad starts in two groups on either side of La Lata (MissionState.Split), with the reward only if one of each
        /// is aboard as it goes (Rules.SplitSafe). No recruit. A group is said by its soldiers (Group), never by its player:
        /// the same words for both players, and for one playing alone.
        /// </summary>
        public static class SplitText
        {
            /// <summary>
            /// A group by name, for the texts here and UI's arrow at the edge («Pierre y Ana · 40 casillas»): first names, all
            /// of them up to three, then «Erik, Marcus y 2 más». `names`: the living, or the whole group once it has fallen.
            /// </summary>
            public static string Group(IReadOnlyList<string> names)
            {
                var first = names.Select((n) => n.Split(' ')[0]).ToList();
                return first.Count > 3 ? $"{first[0]}, {first[1]} y {first.Count - 2} más" : JoinAnd(first);
            }

            /// <summary>
            /// After «sin»: «nadie del grupo de Erik y Marcus», and one alone by name, «Erik» (with 2 seats each group is a
            /// soldier: Arturo, cuadro 95). Here and in EvacStakes.
            /// </summary>
            public static string NobodyOf(IReadOnlyList<string> names) => names.Count == 1 ? Group(names) : $"nadie del grupo de {Group(names)}";

            /// <summary>
            /// The objective's steps (UI's strip): to La Lata with one of each group (done as Rules.SplitShort is null), with a
            /// note under it of the group with nobody there yet (none while neither has); out; and a group fallen
            /// (Rules.SplitLost), in red.
            /// </summary>
            public static string Reach => $"Llegad {Bus.To} con alguien de cada grupo";
            public static string Missing(IReadOnlyList<string> names) => names.Count == 1 ? $"Falta {Group(names)}" : $"Falta el grupo de {Group(names)}";
            public const string Leave = "Salid " + Bus.In;
            public const string LostObjective = "Sin recompensa: salid " + Bus.In;
            /// <summary>The strip's help.</summary>
            public const string ObjectiveTip = "Hay recompensa si " + Bus.Name + " sale con alguien de cada grupo a bordo. Quien no haya subido se queda fuera. Si cae un grupo entero, ya no hay recompensa. La ocultación se pierde para los dos grupos a la vez.";

            /// <summary>Leaving with a group with nobody aboard (EvacSituation.SplitShort): the prompt's title. The text is EvacStakes'.</summary>
            public const string EvacTitle = "Evacuar sin la recompensa";
            /// <summary>The log, as a whole group falls (Rules.SplitLost turning true): the group's names, all of them.</summary>
            public static string Lost(IReadOnlyList<string> names) => $"Sin {NobodyOf(names)}, ya no hay recompensa: salid {Bus.In}.";
            /// <summary>The end's line under «SIN LA RECOMPENSA» (in place of EvacText.WithoutObjective): the group none of whom came out.</summary>
            public static string WithoutGroup(IReadOnlyList<string> names) => $"Salisteis sin {NobodyOf(names)}";
        }

        /// <summary>
        /// Saqueadores (G23.5; Gameplay's .dev-snapshots/gameplay/g23/SAQUEADORES.md; Arturo, cuadros 71, 98 and 99): a camp of
        /// Cuervos (the Glossary's «bandas de saqueadores armados») with its stash in three crates (MissionState.Raiders,
        /// SearchPieces.StashCrate), won as a Saqueo. The stash is the reward, with no 40–80 § besides; left alone, a Saqueo.
        /// One of them seeing a soldier wakes the whole camp (Sim.CampWakes); a noise only sends who hears it to look.
        /// </summary>
        public static class RaidersText
        {
            /// <summary>
            /// The objective's steps (UI's strip): the stash, with the crates still shut of all (`left` of `all`), and «Alijo
            /// robado» once none is; out. Stealing it is up to the squad: without it, the stage is a Saqueo.
            /// </summary>
            public static string Stash(int left, int all) => left <= 0 ? "Alijo robado" : $"Robad el alijo: {(left == 1 ? "queda" : "quedan")} {left} de {all} {(all == 1 ? "caja" : "cajas")}";
            public const string Leave = "Salid " + Bus.In;
            /// <summary>The strip's help.</summary>
            public const string ObjectiveTip = "Las tres cajas del alijo están en el campamento. Se abren desde una casilla de al lado, con un poco de ruido. Un ruido manda a mirar a quien lo oye, y el de la hoguera que lo oye se levanta y ve como los demás. Si uno ve a un soldado, despierta todo el campamento. Mientras la escuadra esté oculta, quien aún no está alerta no nota a quien le llega por detrás o de lado. El golpe silencioso no sirve contra el Cabecilla. Sin robar el alijo, la etapa es un Saqueo.";
            /// <summary>
            /// When the map had no room for the camp (MissionState.Raiders null, one in 90; Producto, 21/11): a Saqueo, with its
            /// objective, under the card still named «SAQUEADORES». UI's: the objective's note, and once in the log at the start.
            /// </summary>
            public const string NoCamp = "Los Cuervos se han ido: no hay campamento. La etapa es un Saqueo.";
            /// <summary>
            /// Over a guard's flag (UI; Arturo, cuadro 104): the camp not woken yet, and woken. Agreed with the unit as
            /// «ESCONDIDO» is (UnitWords.Agree): «DESPREVENIDA». Where a guard looks is a fan on the ground, with no words.
            /// </summary>
            public const string UnawareTag = "DESPREVENIDO";
            public const string AwakeTag = "ALERTA";
        }

        /// <summary>
        /// What is done at a stage, in one line, for its sheet on the map. The figures and names when known: the item to
        /// bring back, the alarm (Alarms), the turn La Lata comes on a Hold, the guard of a Guarida (a template); a Rescate
        /// with Cuervos round the survivor (`crows`; Arturo, 17/11: some are held by them, others hide alone).
        /// </summary>
        public static string StageLine(string kind, string? item = null, string? alarm = null, int? holdTurns = null, string? guard = null, bool crows = false, int? biteTurns = null)
        {
            var item_ = item ?? DefaultItem;
            switch (kind)
            {
                case NodeKinds.Normal:
                    return $"Registrad lo que queráis y salid {Bus.In}.";
                case NodeKinds.Elimination:
                    // La Lata in it too since the cuadro 122 (Gameplay, MissionOptions.EvacAlways): leaving in it is a retreat, the run goes on.
                    return $"Abatid a todo lo que haya. Se gana sin volver {Bus.To}. Irse antes en {Bus.Name} es retirarse: la etapa se supera sin la recompensa.";
                case NodeKinds.Recovery:
                    return $"Recuperad {item_} y volved {Bus.To}.";
                case NodeKinds.Sabotage:
                    return $"Apagad {(alarm != null ? Alarms[alarm].Name : "la alarma")} antes de que termine la cuenta atrás, y volved {Bus.To}.";
                case NodeKinds.Rescue:
                    // Arturo, 17/11: the survivor is in a marked zone, found and talked to (Hablar), and joins.
                    return $"{(crows ? "Los Cuervos retienen a alguien" : "Alguien con vida se esconde")} en una zona marcada del mapa. Llegad a su lado y habladle: se une a la escuadra. Volved todos {Bus.To}.";
                case NodeKinds.Hold:
                    return $"Aguantad hasta que llegue {Bus.Name}{(holdTurns != null ? $", en el turno {holdTurns}" : "")}. Llegan oleadas, cada vez mayores.";
                case NodeKinds.Crossing:
                    return $"Cruzad el mapa: {Bus.Name} espera en el otro extremo. Los muertos empujan por detrás.";
                case NodeKinds.Stealth:
                    return $"Recuperad {item_} y volved {Bus.To} sin que os vean. Sin ser vistos, la recompensa es doble; si os ven, llega una oleada y la recompensa baja a la mitad.";
                case NodeKinds.Elite:
                    return $"{(guard != null && Content.Templates.TryGetValue(guard, out var t) ? $"Un {t.Name}" : "Un muerto especial")} y su grupo guardan la mejor caja del capítulo.";
                case NodeKinds.Key:
                    // G23.1 (Arturo, 18/11 and 19/11, cuadros 71, 73 and 79): won as a Saqueo; the armoury is the prize.
                    return $"La armería de la comisaría está cerrada, con todos sus muebles llenos. Uno de los muertos de uniforme lleva la llave; también se puede volar la pared con una granada. Salid {Bus.In}.";
                case NodeKinds.Bitten:
                    // G23.2 (Arturo, cuadros 73, 87 and 89): BiteTurns from the finding; turned, out without a recruit nor a reward.
                    return $"Alguien mordido se esconde en una zona marcada. Desde que se le encuentra, le quedan {biteTurns ?? Setup.BiteTurns} turnos: dadle el antiviral, que está en uno de los armarios o estantes de medicinas. Volved todos {Bus.To}. Si se convierte, se sale sin recluta ni recompensa.";
                case NodeKinds.Wounded:
                    // G23.3 (Arturo, cuadros 71, 73 and 92): carried to La Lata, which won't leave without them; dead, no recruit nor reward.
                    return $"Alguien con la pierna rota pide ayuda en una zona marcada. No anda: se le lleva a hombros {Bus.To}, y la escuadra se puede turnar. Quien carga anda como mucho {Paths.CarryMoves / Paths.CostOrtho} casillas por turno y no dispara el arma larga. {Bus.Name} no sale mientras siga fuera con vida; si muere, se sale sin recluta ni recompensa.";
                case NodeKinds.Split:
                    // G23.4 (Arturo, cuadros 71, 94 and 95): two groups, the reward with one of each aboard; La Lata goes as always.
                    // Its concealment one for the squad (SEPARADOS.md): this is also what the card says on arrival.
                    return $"La escuadra empieza partida en dos grupos, a los dos lados de {Bus.Name}. Hay recompensa si {Bus.Name} sale con alguien de cada grupo a bordo; quien no haya subido se queda fuera. La ocultación es de toda la escuadra: se pierde para los dos grupos a la vez.";
                case NodeKinds.Raiders:
                    // G23.5 (Arturo, cuadros 71 and 99): the stash the only reward, a Saqueo without it; the round and the stash's
                    // guard, and those at the fire with their eyes on it (Rules.FireWatch; Gameplay, 21/11); hidden, the unaware
                    // blind behind and beside (Los.InNoticeCone), the Golpe silencioso but on the Cabecilla; the camp wakes whole;
                    // their shots too wind the horde (Rules.HordeClock). Also what the card says on arrival, as Separados'.
                    return $"Un campamento de los Cuervos, vivos y armados, guarda su alijo en tres cajas. El alijo es la única recompensa: sin robarlo, la etapa es un Saqueo. Uno hace la ronda y otro vigila el alijo; los de la hoguera miran al fuego y solo ven a {(int)Rules.FireWatch} casillas. Mientras la escuadra esté oculta, no notan a quien les llega por detrás o de lado, y el golpe silencioso los mata sin ruido, salvo al Cabecilla. Si uno ve a un soldado, despierta todo el campamento. Los tiros, también los suyos, atraen a los muertos.";
                case NodeKinds.Charges:
                    // G23 Las cargas (Arturo, cuadros 105 and 109; CARGAS.md): the nest only for now (the pass, later); three charges
                    // in the packs, set from beside the points outside, with a little noise; the third, a count; at 0 the blast,
                    // the squad too, and La Lata goes; leaving before, «superada, sin la recompensa».
                    return $"Volad el nido: un edificio lleno de muertos dormidos. La escuadra lleva {Setup.ChargesCount} cargas en las mochilas; si cae quien lleva una, queda en el suelo. Cada una se pone con una acción, al lado de uno de los puntos marcados por fuera del edificio, y hace un poco de ruido. Al poner la tercera quedan {Setup.ChargesTurns} turnos: entonces estallan los tres puntos, con {Setup.ChargesDamage} de daño a {(int)Setup.ChargesRadius} casillas, también a los vuestros, y {Bus.Name} sale. Quien esté fuera se pierde. Si salís antes de ponerlas todas, la etapa se supera sin la recompensa.";
                case NodeKinds.Refuge:
                    // G22 (cuadro 72): the only way back to the base, but beating the boss.
                    return "Un sitio seguro para descansar: todos se curan del todo. Desde aquí se puede volver a la base.";
                case NodeKinds.Trade:
                    return "Alguien cambia lo que tiene por suministros.";
                case NodeKinds.Event:
                    return "Algo pasa por el camino. Elegid qué hacer.";
                case NodeKinds.Boss:
                    return $"El final del capítulo. Matadlo y salid {Bus.In}.";
                default:
                    throw new ArgumentException($"Unknown stage kind: {kind}");
            }
        }

        /// <summary>
        /// A Rescate's sheet when the squad fills La Lata (Arturo, 17/11): the map warns before going in. Its chip, short.
        /// </summary>
        public const string RescueNoSeat = "No hay plaza libre " + Bus.In + ". Al salir, quien no quepa se queda fuera, y se pierde.";
        public const string RescueNoSeatTag = "Sin plaza libre";

        /// <summary>
        /// Said once when a save of the 0.13.0 is loaded (Arturo, 17/11): the seats go back to 2, and what «Más plazas» cost
        /// is in the stash (`refund`, in §; 0 when it was never done).
        /// </summary>
        public static string SeatsReset(int refund)
        {
            const string seats = "Ahora toda expedición sale con 2 plazas, en moto. La mejora «Más plazas» vuelve a empezar";
            return refund > 0 ? $"{seats}, y los {refund} § que costó están en el almacén." : $"{seats}.";
        }

        /// <summary>
        /// What a special map is like (Arturo, 17/11), for the stage's sheet under «Sitio:»: one building only inside, found
        /// room by room (Sites.IsInside, BuildingRooms), or out of doors as it is; and what a search finds more of there
        /// (RunLoot.SiteExtra; the hospital's, the police station's and the pharmacy's, their furniture: G19). Null for the six
        /// places of always. A weapon's mods are «accesorios», as they were: the base's «mejoras» are another thing.
        /// </summary>
        public static string? SiteLine(string place)
        {
            switch (place)
            {
                case Places.Hospital:
                    return $"{ByRoom} En los armarios, los estantes y los carros de curas salen medicinas y botiquines.";
                case Places.Offices:
                    return $"{ByRoom} Registrando, salen más suministros y accesorios de arma.";
                case Places.Shed:
                    // The shed's floor is one room, found whole (ShedUses.Floor).
                    return "Solo por dentro: la nave se ve entera nada más entrar. Registrando, salen más cargadores, accesorios de arma y suministros.";
                case Places.Police:
                    return $"{ByRoom} En la armería salen armas y cargadores, y en las taquillas, chalecos. Los coches patrulla de delante pueden llevar un arma en el maletero.";
                case Places.Plaza:
                    return "Al aire libre: una plaza con tiendas alrededor, y por sus escaparates se ve dentro. Registrando, salen más suministros y cosas para el cinturón, y en los estantes de la farmacia, medicinas y botiquines.";
                case Places.Camping:
                    return "Al aire libre: un claro cerrado por el pinar, con tiendas de campaña y caravanas en las que se puede entrar. Registrando, sale de todo un poco.";
                default:
                    return null;
            }
        }

        /// <summary>A map that is one building, found room by room, for SiteLine.</summary>
        private const string ByRoom = "Solo por dentro: se descubre sala a sala.";

        /// <summary>
        /// A modifier by name (Modifiers). The bad weather by the one it is, «Tormenta» or «Ventisca» (`snow`: the map
        /// has snow); while it isn't known, «Mal tiempo».
        /// </summary>
        public static string ModifierName(string modifier, bool? snow = null)
        {
            switch (modifier)
            {
                case Modifiers.Night:
                    return Setup.NightLabel;
                case Modifiers.Storm:
                    return snow == null ? "Mal tiempo" : snow.Value ? "Ventisca" : "Tormenta";
                case Modifiers.Awake:
                    return "Horda despierta";
                default:
                    throw new ArgumentException($"Unknown modifier: {modifier}");
            }
        }

        /// <summary>What a modifier does, for the stage's sheet: «Se ve menos y hay más zombis. +50 % de botín.»</summary>
        public static string ModifierLine(string modifier)
        {
            switch (modifier)
            {
                case Modifiers.Night:
                    return "Se ve menos y hay más zombis. +50 % de botín.";
                case Modifiers.Storm:
                    return "Se ve menos y el ruido llega menos lejos. +50 % de botín.";
                case Modifiers.Awake:
                    return "Todos los zombis están alerta desde el principio, y entra una oleada al empezar. +50 % de botín.";
                default:
                    throw new ArgumentException($"Unknown modifier: {modifier}");
            }
        }

        /// <summary>The base's upgrades by name (UpgradeIds).</summary>
        public static readonly IReadOnlyDictionary<string, string> UpgradeNames = new Dictionary<string, string>
        {
            [UpgradeIds.Squad] = "Más plazas",
            [UpgradeIds.Stash] = "Estanterías",
            [UpgradeIds.Pack] = "Mochilas grandes",
            [UpgradeIds.Mend] = "Curas en el camino",
            [UpgradeIds.Sight] = "Mapas de carreteras",
            [UpgradeIds.Haggle] = "Buen trato",
            // The base's ten more (cuadros 164 and 165), the first six (Lore, 25/11: producto/mejoras-base-2026-11-25/TEXTOS.md).
            [UpgradeIds.Training] = "Entrenamiento",
            [UpgradeIds.Route] = "Ruta de vuelta",
            // «Motor silencioso», Lore's, Arturo's yes (cuadro 166; the working name was «Motor afinado»).
            [UpgradeIds.Engine] = "Motor silencioso",
            [UpgradeIds.Garden] = "Huerto",
            [UpgradeIds.Radio] = "Radio",
            [UpgradeIds.Kit] = "Botiquín de salida",
            // The next three (0.14.10; Lore, 25/11, TEXTOS.md).
            [UpgradeIds.Trunk] = "Baúl de La Lata",
            [UpgradeIds.Workshop] = "Taller",
            [UpgradeIds.Scrap] = "Chatarrero",
            // The last (0.14.11).
            [UpgradeIds.Stretcher] = "Camilla",
        };

        /// <summary>
        /// What an upgrade's next level does. `now` and `next`: the soldiers who go out before and after it (Más plazas);
        /// `amount`: the health Curas en el camino heals; `level`: the level it goes to (the base's ten more, cuadro 165).
        /// </summary>
        public static string UpgradeLine(string id, int now = 0, int next = 0, int amount = 0, int level = 1)
        {
            switch (id)
            {
                case UpgradeIds.Squad:
                    // With the transport it brings (Arturo, 17/11: 2 on the motorbike, then 4, 6 and 8).
                    return $"Salen {next} soldados en vez de {now}, en {RunTransport(next).A}.";
                case UpgradeIds.Stash:
                    return "El almacén tiene 2 filas más.";
                case UpgradeIds.Pack:
                    return "Cada mochila tiene una fila más.";
                case UpgradeIds.Mend:
                    return $"Tras cada etapa, todos recuperan {amount} de salud.";
                case UpgradeIds.Sight:
                    return "Se ve una etapa más del camino.";
                case UpgradeIds.Haggle:
                    // M10 (cuadro 141): what is offered pays more too (Content.SellPrice).
                    return $"En el Trueque, todo cuesta un {Content.HaggleDiscount} % menos, y lo que ofrecéis se paga un {Content.HaggleSale} % más.";
                // The base's ten more (cuadros 164 and 165; Lore, 25/11, TEXTOS.md).
                case UpgradeIds.Training:
                {
                    // Runs.Trained: a point a level, to a survivor who joins and to a recruit (Runs.NewSoldier). Never «barra»
                    // here (Arturo, cuadro 166): the player sees «puntos por gastar».
                    var points = Math.Max(1, level);
                    return $"Quien se une al Bastión, un superviviente o un recluta, llega con {points} {(points == 1 ? "punto" : "puntos")} de habilidad por gastar.";
                }
                case UpgradeIds.Route:
                    return "Desde un Trueque también se puede volver a la base, no solo desde un Refugio.";
                case UpgradeIds.Engine:
                    return $"La horda no llega hasta el turno {Runs.EngineHordeFrom[Math.Max(1, Math.Min(level, Runs.EngineHordeFrom.Count)) - 1]}: hasta entonces no entran muertos, ni los de cada turno ni los que trae el ruido.";
                case UpgradeIds.Garden:
                    return $"Al volver de cada expedición, el almacén recibe {Runs.GardenSupplies[Math.Max(1, Math.Min(level, Runs.GardenSupplies.Count)) - 1]} §, aunque haya ido mal.";
                case UpgradeIds.Radio:
                    return "En el mapa se ven los avisos de todas las etapas del capítulo (de noche, mal tiempo, horda despierta), también de las que aún son «?», y una etapa más del camino.";
                // The next three (0.14.10; Lore, 25/11, TEXTOS.md).
                case UpgradeIds.Trunk:
                    // Gears.Target: moved between stages, never in a fight; RunRules.EndRun: home also with the squad fallen.
                    return $"La Lata lleva un baúl de 4×{Content.TrunkRows[Math.Max(1, Math.Min(level, Content.TrunkRows.Count)) - 1]} casillas: entre etapa y etapa se deja y se coge lo que queráis. Al volver a la base, lo que lleve pasa al almacén, también si cae la escuadra.";
                case UpgradeIds.Workshop:
                {
                    var w = Content.WorkshopWares;
                    return level >= 2
                        ? $"El taller hace también accesorios: mira, silenciador, culata y cargador ampliado, a {w[WorkshopIds.Scope].Price} § cada uno."
                        : $"En la base se hacen, con los § del almacén, cargadores ({w[WorkshopIds.Mags].Price} §), flechas ({w[WorkshopIds.Arrows].Price} § las {w[WorkshopIds.Arrows].Count}) y cócteles molotov ({w[WorkshopIds.Molotov].Price} §).";
                }
                case UpgradeIds.Scrap:
                    // Content.ScrapPrice: half the Trade's, without «Buen trato».
                    return "En la base, lo del almacén se puede cambiar por §: se paga la mitad que en el Trueque, sin contar «Buen trato». Lo que no se cambia en el Trueque, aquí tampoco.";
                case UpgradeIds.Stretcher:
                    // Rules.StretcherTakes, DownedEvent, RunFights: carried as El herido's wounded, dead if hurt again or left out.
                    return $"Una vez por expedición, el primer soldado que cae no muere: queda malherido en el suelo. Si lo lleváis a hombros {Bus.To}, sale del combate con 1 de salud. Si lo vuelven a herir antes, o se queda fuera cuando sale {Bus.Name}, muere.";
                case UpgradeIds.Kit:
                    // Runs.Issue: into a free place of the belt, if there is none of it there; Gear.Issued, never offered.
                    return level >= 2
                        ? "Cada soldado sale también con un cóctel molotov en el cinturón, si no lleva ya uno y hay sitio. No se puede cambiar."
                        : "Cada soldado sale de la base con un botiquín en el cinturón, si no lleva ya uno y hay sitio. No se puede cambiar.";
                default:
                    throw new ArgumentException($"Unknown upgrade: {id}");
            }
        }

        /// <summary>
        /// N9 (Arturo, 22/11, cuadros 114 and 118): the game's level, chosen when it is made and never changed (Levels.All). What
        /// changes: the dead, the horde, the supplies found and paid; in Supervivencia the wounds come home too. «Dificultad» is
        /// only this; a stage's is «Peligro» (cuadro 115). Without the numbers, which will be tuned by playing.
        /// </summary>
        public static class GameLevelText
        {
            /// <summary>Over the four to choose from, and under them, what goes for all.</summary>
            public const string Title = "Dificultad";
            public const string Fixed = "Se elige al crear la partida y no se puede cambiar.";
            public const string AllLose = "En todas: si no queda ningún soldado vivo, ni en la base, la partida se pierde.";

            /// <summary>The levels by name (LevelIds).</summary>
            public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
            {
                [LevelIds.Easy] = "Fácil",
                [LevelIds.Medium] = "Medio",
                [LevelIds.Hard] = "Difícil",
                [LevelIds.Survival] = "Supervivencia",
            };

            /// <summary>What each one changes, a line each (against Difícil, the one the others are made from).</summary>
            public static readonly IReadOnlyDictionary<string, string> Lines = new Dictionary<string, string>
            {
                [LevelIds.Easy] = "Menos muertos, una horda más lenta y más suministros.",
                [LevelIds.Medium] = "Algo menos de muertos, una horda algo más lenta y algo más de suministros.",
                [LevelIds.Hard] = "Los muertos, la horda y los suministros normales. Los otros niveles cambian a partir de este.",
                [LevelIds.Survival] = "Más muertos, una horda más rápida y menos suministros. Las heridas no se curan al volver a la base: solo en un Refugio, con medicinas o con «Curas en el camino».",
            };

            /// <summary>A game's level by name (null: Difícil, every game made before).</summary>
            public static string NameOf(string? level) => level != null && Names.TryGetValue(level, out var name) ? name : Names[LevelIds.Hard];
        }

        /// <summary>
        /// N8 (Arturo, 22/11, cuadros 113 and 118): nobody alive in the game, the base too, and it is lost, whole (the stash, the
        /// upgrades and the chapter go with it); no recruit given. The end's window (StrategyPresenter.End, for a game of runs),
        /// and the notice for whoever is left with none while the partner has some (no one loses: Runs.HandOver, Runs.Controls).
        /// </summary>
        public static class LostText
        {
            public const string Kicker = "Fin de la partida";
            public const string Title = "Partida perdida";
            public const string Text = "No queda ningún soldado vivo, ni en la expedición ni en la base. Se pierde todo: el almacén, las mejoras y el capítulo. Para volver a jugar, empezad una partida nueva.";

            /// <summary>
            /// At the base, for a player with no soldiers of their own: with the partner in, half the squad is theirs to lead when
            /// it sets out; alone, they may take the partner's out and lead them.
            /// </summary>
            public static string NoneOfYourOwn(string partner, bool partnerIn) => partnerIn
                ? $"No te quedan soldados. Al partir, mandas en la mitad de la escuadra de {partner}."
                : $"No te quedan soldados. Puedes sacar a los de {partner} y mandarlos mientras no esté.";
        }

        /// <summary>
        /// Supervivencia (N9): «Volver a la base» without the healing (ExpeditionText.ReturnHelp), and a wounded soldier at the base,
        /// who sets out with what it came home with (Runs.HpOf, CampaignSoldier.Hurt).
        /// </summary>
        public static class WoundsText
        {
            public const string ReturnHelp = "Acaba la expedición. Lo que llevan las mochilas se guarda. Los heridos siguen heridos.";
            public static string Hurt(int hp, int max) => $"Herido: sale con {hp} de {max} de salud. Se cura en un Refugio o con medicinas.";
        }
    }
}
