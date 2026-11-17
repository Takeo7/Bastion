using System;
using System.Collections.Generic;

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
            /// <summary>The button that starts one (StartRunCommand): «Expedición» is long for a button.</summary>
            public const string Start = "Salir";
            public static string StartHelp(int chapter) => $"Sale la escuadra: empieza una expedición del capítulo {chapter}.";
            /// <summary>The button that ends one, back to the base.</summary>
            public const string Return = "Volver a la base";
            public const string ReturnHelp = "Acaba la expedición. Todos se curan, y lo que llevan las mochilas se guarda.";
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
            [NodeKinds.Hack] = "Arreglar la radio",
            [NodeKinds.Rescue] = "Rescate",
            [NodeKinds.Hold] = "Asedio",
            [NodeKinds.Crossing] = "Travesía",
            [NodeKinds.Stealth] = "Sigilo",
            [NodeKinds.Elite] = "Guarida",
            [NodeKinds.Refuge] = "Refugio",
            [NodeKinds.Trade] = "Trueque",
            [NodeKinds.Event] = "Suceso",
            [NodeKinds.Survivor] = "Superviviente",
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
        /// What is done at a stage, in one line, for its sheet on the map. The figures and names when known: the item to
        /// bring back, the alarm (Alarms), the turn La Lata comes on a Hold, the guard of a Guarida (a template).
        /// </summary>
        public static string StageLine(string kind, string? item = null, string? alarm = null, int? holdTurns = null, string? guard = null)
        {
            var item_ = item ?? DefaultItem;
            switch (kind)
            {
                case NodeKinds.Normal:
                    return $"Registrad lo que queráis y salid {Bus.In}.";
                case NodeKinds.Elimination:
                    return $"Abatid a todo lo que haya. Se gana sin volver {Bus.To}.";
                case NodeKinds.Recovery:
                    return $"Recuperad {item_} y volved {Bus.To}.";
                case NodeKinds.Sabotage:
                    return $"Apagad {(alarm != null ? Alarms[alarm].Name : "la alarma")} antes de que termine la cuenta atrás, y volved {Bus.To}.";
                case NodeKinds.Hack:
                    return $"Poned en marcha el equipo y volved {Bus.To}. Si falla, salta la alarma.";
                case NodeKinds.Rescue:
                    return $"Sacad con vida a la persona atrapada y volved {Bus.To}. Se une a vosotros.";
                case NodeKinds.Hold:
                    return $"Aguantad hasta que llegue {Bus.Name}{(holdTurns != null ? $", en el turno {holdTurns}" : "")}. Llegan oleadas, cada vez mayores.";
                case NodeKinds.Crossing:
                    return $"Cruzad el mapa: {Bus.Name} espera en el otro extremo. Los muertos empujan por detrás.";
                case NodeKinds.Stealth:
                    return $"Recuperad {item_} y volved {Bus.To} sin que os vean. Sin ser vistos, la recompensa es doble; si os ven, llega una oleada y la recompensa baja a la mitad.";
                case NodeKinds.Elite:
                    return $"{(guard != null && Content.Templates.TryGetValue(guard, out var t) ? $"Un {t.Name}" : "Un muerto especial")} y su grupo guardan la mejor caja del capítulo.";
                case NodeKinds.Refuge:
                    return "Un sitio seguro para descansar: todos se curan del todo.";
                case NodeKinds.Trade:
                    return "Alguien cambia lo que tiene por suministros.";
                case NodeKinds.Event:
                    return "Algo pasa por el camino. Elegid qué hacer.";
                case NodeKinds.Survivor:
                    return "Alguien pide unirse a vosotros. Decidís si se une.";
                case NodeKinds.Boss:
                    return $"El final del capítulo. Matadlo y salid {Bus.In}.";
                default:
                    throw new ArgumentException($"Unknown stage kind: {kind}");
            }
        }

        /// <summary>
        /// What a special map is like (Arturo, 17/11), for the stage's sheet under «Sitio:»: one building only inside, found
        /// room by room (Sites.IsInside, BuildingRooms), or out of doors as it is; and what a search finds more of there
        /// (RunLoot.SiteExtra; the hospital's, its furniture). Null for the six places of always. A weapon's mods are
        /// «accesorios», as they were: the base's «mejoras» are another thing.
        /// </summary>
        public static string? SiteLine(string place)
        {
            switch (place)
            {
                case Places.Hospital:
                    return $"{ByRoom} Registrando, salen más medicinas y botiquines.";
                case Places.Offices:
                    return $"{ByRoom} Registrando, salen más suministros y accesorios de arma.";
                case Places.Shed:
                    // The shed's floor is one room, found whole (ShedUses.Floor).
                    return "Solo por dentro: la nave se ve entera nada más entrar. Registrando, salen más cargadores, accesorios de arma y suministros.";
                case Places.Police:
                    return $"{ByRoom} Registrando, salen más armas, cargadores y chalecos.";
                case Places.Plaza:
                    return "Al aire libre: una plaza con tiendas alrededor, y por sus escaparates se ve dentro. Registrando, salen más suministros y cosas para el cinturón, y en la farmacia, medicinas.";
                // H9 §5, before its Places id: «camping», as UI reads it.
                case "camping":
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
        };

        /// <summary>
        /// What an upgrade's next level does. `now` and `next`: the soldiers who go out before and after it (Más plazas);
        /// `amount`: the health Curas en el camino heals.
        /// </summary>
        public static string UpgradeLine(string id, int now = 0, int next = 0, int amount = 0)
        {
            switch (id)
            {
                case UpgradeIds.Squad:
                    return $"Salen {next} soldados en vez de {now}.";
                case UpgradeIds.Stash:
                    return "El almacén tiene 2 filas más.";
                case UpgradeIds.Pack:
                    return "Cada mochila tiene una fila más.";
                case UpgradeIds.Mend:
                    return $"Tras cada etapa, todos recuperan {amount} de salud.";
                case UpgradeIds.Sight:
                    return "Se ve una etapa más del camino.";
                case UpgradeIds.Haggle:
                    return $"En el Trueque, todo cuesta un {Content.HaggleDiscount} % menos.";
                default:
                    throw new ArgumentException($"Unknown upgrade: {id}");
            }
        }
    }
}
