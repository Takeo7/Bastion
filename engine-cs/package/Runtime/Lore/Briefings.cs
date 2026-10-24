using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ---------------------------------------------------------------- briefings

        /// <summary>An ordinary sabotage's report, by what sounds (MissionOffer.alarm).</summary>
        private static readonly IReadOnlyDictionary<string, string> SabotageBriefings = new Dictionary<string, string>
        {
            ["police"] = "Los Cuervos dejaron allí **un coche patrulla con la sirena puesta** y se fueron. Mientras suene, los muertos de toda la comarca vendrán hacia aquí. ==Apagadla antes de que termine la cuenta atrás.==",
            ["ambulance"] = "Allí hay **una ambulancia con la sirena atascada**, y la batería aguanta. Mientras suene, los muertos de toda la comarca vendrán hacia aquí. ==Apagadla antes de que termine la cuenta atrás.==",
            ["factory"] = "La alarma de **una nave** salta allí cada noche, sola, con la batería. Mientras suene, los muertos de toda la comarca vendrán hacia aquí. ==Apagadla antes de que termine la cuenta atrás.==",
        };

        /// <summary>Several reports per mission type; each offer always gets the same one.</summary>
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> KindBriefings = new Dictionary<string, IReadOnlyList<string>>
        {
            ["elimination"] = new[]
            {
                "Hace semanas que nadie puede acercarse: está **lleno de muertos**, y es paso obligado hacia la sierra. ==Que no quede ninguno.==",
                "Una **manada de zombis** ha cortado la única carretera con agua potable. ==Limpiad la zona.==",
                "Los muertos se han quedado allí, y por ahí pasa **la ruta de la sierra**. ==Abatidlos a todos.==",
            },
            ["recovery"] = new[]
            {
                $"Allí quedó **el mapa de los pozos** de la comarca. Sin él, el agua del verano es una apuesta. ==Recuperadlo y volved {Bus.To}.==",
                "Allí cayó **un dron** que alguien usaba para vigilar las carreteras. Lo que grabó dice por dónde pasan los muertos. ==Recuperadlo antes de que se acabe el tiempo.==",
                "Los Cuervos escondieron allí su **libro de cuentas** y no volvieron a por él: los muertos llegaron antes. Dice quién les paga y por dónde pasan. ==Traedlo.==",
            },
            ["sabotage"] = new[]
            {
                "Algo suena allí cada noche, y **los muertos de toda la comarca** acuden. ==Que deje de sonar antes de que termine la cuenta atrás.==",
                "Cada noche suena **una alarma** junto a nuestras rutas. ==Apagadla a tiempo.==",
            },
            ["hack"] = new[]
            {
                "Hay **una radio de emergencia** en la zona, muerta desde el invierno. ==Arregladla== y oiremos a quien queda. Si falla, **el ruido** atraerá a todo lo que ande cerca.",
                "Los Cuervos montaron allí **un repetidor** y hablan por él. ==Ponedlo a nuestro favor== y sabremos qué traman. Si falla, **saltará la alarma**.",
                "Allí queda **un equipo de radio** entero. ==Ponedlo en marcha.== Si falla, **el ruido** traerá compañía.",
            },
            ["rescue"] = new[]
            {
                "Los Cuervos se han llevado {a}. Si llegan a su campamento, **no habrá otra ocasión**. ==Que salga con vida.==",
                "Los Cuervos retienen {a} y piden gasolina a cambio. **No vamos a pagar.** ==Que vuelva con vida.==",
                "Los Cuervos han parado allí con sus prisioneros. Entre ellos va **{who}**, que sabe cosas que nos hacen falta. ==Que salga con vida.==",
            },
            ["scavenge"] = ScavengeBriefings.All,
        };

        /// <summary>lore.ts hash: 31 × h + each UTF-16 unit, kept to 32 bits (`>>> 0`).</summary>
        private static uint BriefingHash(string text)
        {
            uint h = 0;
            foreach (var ch in text) h = unchecked(h * 31 + ch);
            return h;
        }

        /// <summary>What picks an ordinary operation's report: the same offer always gets the same one.</summary>
        private static long BriefingSeed(MissionOffer o)
        {
            // A sum of numbers in TypeScript, past 32 bits: a long here.
            return (long)BriefingHash(o.Id) + (o.Map.Kind == MapSourceKinds.Generated ? o.Map.Seed ?? 0 : 0);
        }

        /// <summary>
        /// What each ordinary recovery carries out, in the order of its reports
        /// (BRIEFINGS.recovery), and whether its report puts it out in the open
        /// rather than in a building (docs/PRUEBA-2026-10-14.md #27): the pharmacy's
        /// medicines and the Cuervos' warehouse are indoors, the courier's backpack
        /// lies in the street.
        /// Never anything a search turns up (playtest 18/10 #13): the objective must not look like the loot.
        /// </summary>
        private static readonly IReadOnlyList<(string Item, bool Outdoors)> RecoveryItems = new[] { ("el mapa de los pozos", false), ("el dron", true), ("el libro de cuentas", false) };

        private static (string Item, bool Outdoors)? OrdinaryRecovery(MissionOffer o)
        {
            if (o.Kind != "recovery" || (o.Story ?? 0) != 0 || !string.IsNullOrEmpty(o.Quest) || o.Final || !string.IsNullOrEmpty(o.Facility)) return null;
            return RecoveryItems[(int)(BriefingSeed(o) % RecoveryItems.Count)];
        }

        /// <summary>The objective an ordinary recovery names, as its report does; null for any other operation.</summary>
        public static string? RecoveryItem(MissionOffer o)
        {
            return OrdinaryRecovery(o)?.Item;
        }

        /// <summary>Whether an ordinary recovery's objective lies out in the open (its report says so); false indoors or for any other operation.</summary>
        public static bool RecoveryOutdoors(MissionOffer o)
        {
            return OrdinaryRecovery(o)?.Outdoors ?? false;
        }

        /// <summary>Who signs an operation's report, in pen under it.</summary>
        public static string BriefingSignature(MissionOffer o)
        {
            if (o.Story == 1) return "";
            return (o.Story ?? 0) != 0 ? Story[o.Story!.Value - 1].Signature : "— N. A.";
        }

        /// <summary>
        /// The marks in a mission's presentation (Ana, 22/10 #28): **key words** in
        /// white bold, ==the objective== in yellow (UI renders them). Where a text is
        /// shown plain, without them.
        /// </summary>
        public static string PlainMarks(string text)
        {
            return Regex.Replace(text, "\\*\\*|==", "");
        }

        /// <summary>Morse's report for an operation, signed by the region's ally. `simon`: StoryState.simon (SimonChoices).</summary>
        public static string MissionBriefing(MissionOffer o, int level, string? simon = null)
        {
            var text = BriefingText(o, level, simon);
            return o.Night == true ? $"{text} {NightText.Briefing}" : text;
        }

        private static string BriefingText(MissionOffer o, int level, string? simon)
        {
            if ((o.Story ?? 0) != 0) return StoryBriefing(o.Story!.Value, simon);
            if (o.Final)
            {
                return "La Marea está en la sierra y la Congregación la empuja desde su campamento. La Lata se la llevará con la sirena; vosotros, al campamento. ==Abatid a todo lo que haya.== **No habrá otra oportunidad.**";
            }
            if (!string.IsNullOrEmpty(o.Facility))
            {
                var effect = level >= PartituraLevel ? "acerca la Marea un kilómetro cada 3 misiones" : "hace crecer la infestación cada 3 misiones";
                return $"Este Nido {effect}. Lo mantiene vivo **la alarma de una nave**, que no deja de sonar: ==apagadla antes de que termine la cuenta atrás.==";
            }
            if (!KindBriefings.TryGetValue(o.Kind, out var options)) throw new ArgumentException($"Unknown mission kind: {o.Kind}");
            var seed = BriefingSeed(o);
            var ally = RegionLores[o.Region].Ally;
            // What sounds, and who is held, once the offer says (docs/PRUEBA-2026-10-15.md #14, #20).
            var who = !string.IsNullOrEmpty(o.Vip) ? Titled(o.Vip!) : "alguien";
            var report = o.Kind == "sabotage" && !string.IsNullOrEmpty(o.Alarm) ? SabotageBriefings[o.Alarm!] : options[(int)(seed % options.Count)];
            // String.prototype.replace with a string: the first one only.
            var text = ReplaceFirst(ReplaceFirst(report, "{a}", who.StartsWith("el ", StringComparison.Ordinal) ? $"al {who.Substring(3)}" : $"a {who}"), "{who}", who);
            return $"Aviso {(ally.StartsWith("el ", StringComparison.Ordinal) ? $"del {ally.Substring(3)}" : $"de {ally}")}. {text}";
        }

        private static string ReplaceFirst(string text, string what, string with)
        {
            var at = text.IndexOf(what, StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(0, at) + with + text.Substring(at + what.Length);
        }
    }
}
