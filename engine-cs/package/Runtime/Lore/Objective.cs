using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // --------------------------------------------------------------- objective

        /// <summary>What a mission's objective is called when nothing names it (a skirmish).</summary>
        public const string DefaultItem = "la carga";

        /// <summary>The item down to its article and noun: «el registro de prisioneros» → «el registro».</summary>
        private static string ShortItem(string item)
        {
            var words = item.Split(' ');
            return Regex.IsMatch(words[0], "^(el|la|los|las)$") ? string.Join(" ", words, 0, Math.Min(2, words.Length)) : words[0];
        }

        /// <summary>The carrier's tag, short and in capitals, over their head and on their sheet: «MEDICINAS», «REGISTRO».</summary>
        public static string ItemTag(string item)
        {
            return Regex.Replace(item, "^(el|la|los|las) ", "").Split(' ')[0].ToUpperInvariant();
        }

        /// <summary>Floating over whoever picks it up: «RECOGE LAS MEDICINAS».</summary>
        public static string ItemTakenFloat(string item)
        {
            return $"RECOGE {ShortItem(item).ToUpperInvariant()}";
        }

        /// <summary>The log line when it is picked up: «Ana recoge las medicinas.»</summary>
        public static string ItemTakenLine(string who, string item)
        {
            return $"{who} recoge {item}.";
        }

        /// <summary>Floating where it falls when its carrier goes down: «MEDICINAS AL SUELO».</summary>
        public static string ItemDroppedFloat(string item)
        {
            return $"{ItemTag(item)} AL SUELO";
        }

        /// <summary>The log line when it falls: «Las medicinas han caído al suelo.», «El diario ha caído al suelo.»</summary>
        public static string ItemDroppedLine(string item)
        {
            return $"{Capital(item)} {(Regex.IsMatch(item, "^(los|las) ") ? "han" : "ha")} caído al suelo.";
        }

        /// <summary>Where the objective is, for the strip over the battlefield (docs/PRUEBA-2026-10-14.md #23): "loose", "carried", "aboard" or "out".</summary>
        public static class ItemPhase
        {
            public const string Loose = "loose";
            public const string Carried = "carried";
            public const string Aboard = "aboard";
            public const string Out = "out";
        }

        /// <summary>
        /// The objective strip for an item: «Recuperad las medicinas y volved a La
        /// Mula», «Ana lleva las medicinas: a La Lata», «Las medicinas, en La Lata: que
        /// suba el resto», «Las medicinas, a salvo». `carrier` names who has it.
        /// </summary>
        public static string ItemObjective(string item, string phase, string? carrier = null)
        {
            var item_ = Capital(item);
            switch (phase)
            {
                case ItemPhase.Loose:
                    return $"Recuperad {item} y volved {Bus.To}";
                case ItemPhase.Carried:
                    return !string.IsNullOrEmpty(carrier) ? $"{carrier} lleva {item}: {Bus.To}" : $"Llevad {item} {Bus.To}";
                case ItemPhase.Aboard:
                    return $"{item_}, {Bus.In}: que suba el resto";
                case ItemPhase.Out:
                    return $"{item_}, a salvo";
                default:
                    throw new ArgumentException($"Unknown item phase: {phase}");
            }
        }

        /// <summary>
        /// A name with a title takes its article: «la Dra. Irina Sokolova», «el Ing.
        /// Kenji Mori», «la alcaldesa Mara Velasco»; a bare name doesn't.
        /// </summary>
        private static string Titled(string name)
        {
            var m = Regex.Match(name, "^(Dra\\.|Dr\\.|Ing\\.|Alcaldesa|Agente) ");
            if (!m.Success) return name;
            var title = m.Groups[1].Value;
            var female = title == "Dra." || title == "Alcaldesa" || (NamedSex.TryGetValue(name, out var sex) && sex == "f");
            return $"{(female ? "la" : "el")} {(title.EndsWith(".") ? title : title.ToLowerInvariant())} {name.Substring(m.Length)}";
        }

        /// <summary>
        /// The same for a rescue: «Llegad hasta la Dra. Irina Sokolova» while still
        /// held, «Llevad a la Dra. Irina Sokolova a La Lata», «La Dra. Irina Sokolova,
        /// en La Lata: que suba el resto», «…, a salvo». `phase`: "captive", "loose", "aboard" or "out".
        /// </summary>
        public static string VipObjective(string vip, string phase)
        {
            var who = Titled(vip);
            if (phase == "captive") return $"Llegad hasta {who}";
            if (phase == "loose") return $"Llevad {(who.StartsWith("el ") ? $"al {who.Substring(3)}" : $"a {who}")} {Bus.To}";
            var who_ = Capital(who);
            return phase == "aboard" ? $"{who_}, {Bus.In}: que suba el resto" : $"{who_}, a salvo";
        }

        /// <summary>A sabotage's objective strip, by what sounds: «Apagad la sirena del coche patrulla».</summary>
        public static string AlarmObjective(string? alarm = null)
        {
            return $"Apagad {(alarm != null ? Alarms[alarm].Name : "la alarma")}";
        }

        /// <summary>The log line when it stops: «Ana apaga la sirena de la ambulancia.»</summary>
        public static string AlarmDoneLine(string who, string? alarm = null)
        {
            return $"{who} apaga {(alarm != null ? Alarms[alarm].Name : "la alarma")}.";
        }

        /// <summary>The strip once the objective of a sabotage or a repair is done: back to the bus.</summary>
        public static readonly IReadOnlyDictionary<string, string> ObjectiveDone = new Dictionary<string, string>
        {
            ["sabotage"] = $"Ya no suena: volved {Bus.To}",
            ["hack"] = $"Equipo en marcha: volved {Bus.To}",
        };

        /// <summary>What sounds, as a sentence takes it (`Name`) and as it is introduced (`Find`).</summary>
        public sealed class AlarmText
        {
            public AlarmText(string name, string find)
            {
                Name = name;
                Find = find;
            }

            public string Name { get; }
            public string Find { get; }
        }

        /// <summary>
        /// What sounds in a sabotage (docs/PRUEBA-2026-10-15.md #14): a police car the
        /// Cuervos took the siren from, an ambulance whose siren stuck, or a factory's
        /// alarm running on its battery. `name` for a sentence, `find` to introduce it.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, AlarmText> Alarms = new Dictionary<string, AlarmText>
        {
            ["police"] = new AlarmText("la sirena del coche patrulla", "un coche patrulla con la sirena puesta"),
            ["ambulance"] = new AlarmText("la sirena de la ambulancia", "una ambulancia con la sirena atascada"),
            ["factory"] = new AlarmText("la alarma de la nave", "la alarma de una nave, que salta sola con la batería"),
        };

        /// <summary>
        /// What an operation is, in one line, for «Qué hace» (docs/PRUEBA-2026-10-15.md
        /// #4): «Recuperar las medicinas y volver a La Lata», «Sacar a la alcaldesa Mara
        /// Velasco con vida…». The item, the hostage and the alarm when known (campaign
        /// missionInfo); without them, words that still hold. The same line for story
        /// chapters and quest steps: none of them says more than the briefing.
        /// </summary>
        public static string OperationAim(string kind, string? item = null, string? vip = null, string? alarm = null, bool facility = false, bool final = false)
        {
            if (final) return "Asaltar el campamento de la Congregación y abatir a todo lo que haya.";
            var alarm_ = alarm != null ? Alarms[alarm].Name : "la alarma";
            if (facility) return $"Apagar {alarm_} que alimenta el Nido antes de que termine la cuenta atrás, y volver {Bus.To}.";
            switch (kind)
            {
                case "elimination":
                    return $"Abatir a todo lo que haya. Se gana sin volver {Bus.To}.";
                case "recovery":
                    return $"Recuperar {item ?? DefaultItem} y volver {Bus.To}.";
                case "sabotage":
                    return $"Apagar {alarm_} antes de que termine la cuenta atrás, y volver {Bus.To}.";
                case "hack":
                    return $"Poner en marcha el equipo y volver {Bus.To}. Si falla, salta la alarma.";
                case "rescue":
                {
                    var who = vip != null ? Titled(vip) : "alguien";
                    return $"Sacar {(who.StartsWith("el ") ? $"al {who.Substring(3)}" : $"a {who}")} con vida y volver {Bus.To}.";
                }
                case "scavenge":
                    return $"Registrar lo que se pueda y volver {Bus.To}. Llegan muertos por los bordes.";
                default:
                    throw new ArgumentException($"Unknown mission kind: {kind}");
            }
        }

        /// <summary>The «Sin contacto» tag on a Nest: «Contacta por radio con los del Hospital desde el Mapa (50 §) para poder asaltarlo.»</summary>
        public static string NestNoContactTip(string id, int cost)
        {
            return $"Contacta por radio con {RegionLores[id].Ally} desde el Mapa ({cost} §) para poder asaltarlo.";
        }

        /// <summary>
        /// The tooltip of the Mapa's «Nidos» head: the glossary's Nido, with the doom track by the name it has at `level`.
        /// Not in lore.ts, which shows GLOSSARY.Nido, and that names the Marea before level 4, when nobody knows of it yet.
        /// </summary>
        public static string NestTip(int level)
        {
            return $"Sitio donde se juntan los zombis. Hace crecer {DoomSubject(level)}; quemarlo la hace retroceder.";
        }

        /// <summary>«Qué hace» for a contact: who answers, what they send and what it opens.</summary>
        public static string ContactAim(string id, int income)
        {
            return $"Cerrar un trato por radio con {RegionLores[id].Ally}: {income} § en cada convoy, en un punto de cambio, mientras su zona no caiga. Y se pueden asaltar sus Nidos.";
        }

        /// <summary>«Qué hace» for listening to a rumour: what it may turn out to be.</summary>
        public static string RumorAim(string zone)
        {
            return $"Escuchar el rumor {OfZone(zone)} unas misiones: puede ser un encargo, una misión mejor pagada, un alijo o nada.";
        }

        /// <summary>«Qué hace» for an order to the Taller (progression.ts CRAFTS): its `kind` and `the`.</summary>
        public static string CraftAim(string kind, string the)
        {
            if (kind == "arms") return $"Hacer {the}: va a la Armería y se reparte desde allí.";
            if (kind == "ammo") return $"Hacer {the}: un cargador de repuesto más para cada soldado que salga.";
            return $"Hacer {the}: para todos, hasta lo que permita su rango.";
        }

        /// <summary>The partner's notice: «Ana lleva las medicinas».</summary>
        public static string ItemPartnerLine(string who, string item)
        {
            return $"{who} lleva {item}";
        }
    }
}
