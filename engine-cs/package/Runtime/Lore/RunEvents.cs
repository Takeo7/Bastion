using System;
using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ----------------------------------------------------------- run events

        /// <summary>One option of a Suceso: its button, and the line once chosen.</summary>
        public sealed class RunChoiceText
        {
            private readonly Func<string, string, string> result;

            public RunChoiceText(string label, Func<string, string, string> result)
            {
                Label = label;
                this.result = result;
            }

            /// <summary>The button, without figures: what it does in figures goes by it (RunEventEffects).</summary>
            public string Label { get; }

            /// <summary>The line once chosen: `who`, the soldier it fell to; `survivor`, whoever joined (StopState.Outcome).</summary>
            public string Result(string? who = null, string? survivor = null)
            {
                return result(who ?? "", survivor ?? "");
            }
        }

        /// <summary>A Suceso of an expedition (H4): its title, what happens and its two options.</summary>
        public sealed class RunEventText
        {
            public RunEventText(string title, string text, RunChoiceText first, RunChoiceText second)
            {
                Title = title;
                Text = text;
                Options = new[] { first, second };
            }

            public string Title { get; }
            public string Text { get; }
            public IReadOnlyList<RunChoiceText> Options { get; }
        }

        /// <summary>
        /// The Sucesos (Gameplay, 16/11; .dev-snapshots/lore/runs/SUCESOS.md), by the ids of Content.RunEvents, which holds
        /// what each option does. No figures here: the button says them in short (RunEventEffects), so that they are only
        /// Content's.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, RunEventText> RunEvents = new Dictionary<string, RunEventText>
        {
            ["farmacia"] = new RunEventText("La farmacia", "Una farmacia con la persiana medio bajada. Dentro se oye algo que se arrastra.",
                new RunChoiceText("Entrar a por lo que quede", (who, _) => $"Había un muerto atrapado bajo un estante. {who} sale con un botiquín y un mordisco."),
                new RunChoiceText("Seguir de largo", (_, _) => "Lo que se arrastra se queda dentro.")),
            ["gasolinera"] = new RunEventText("La gasolinera", "Un coche sigue en el surtidor de una gasolinera. Al depósito le queda gasolina.",
                new RunChoiceText("Sacarla con la bomba", (_, _) => "La bomba hace ruido, y se oye lejos."),
                new RunChoiceText("Dejarla", (_, _) => "Seguís sin hacer ruido.")),
            ["herido"] = new RunEventText("Alguien herido", "Junto a la carretera, alguien con una pierna vendada levanta la mano. Lleva una pistola.",
                // Arturo, 17/11: recruits come only from a Rescate; this one marks one ahead.
                new RunChoiceText("Que os espere más adelante", (_, _) => "Le indicáis una casa del camino. Allí esperará a que paséis."),
                new RunChoiceText("Cambiarle la pistola por suministros", (_, _) => "Os da la pistola y se va cojeando.")),
            ["campamento"] = new RunEventText("El campamento", "Un campamento vacío junto al río. Las tiendas siguen montadas.",
                new RunChoiceText("Descansar un rato", (_, _) => "Descansáis, y se os hace tarde."),
                new RunChoiceText("Registrarlo deprisa", (_, _) => "Entre las tiendas quedan suministros.")),
            ["puente"] = new RunEventText("El puente", "El puente está cortado con coches. O se aparta uno, o se da la vuelta por el monte.",
                new RunChoiceText("Empujar un coche", (who, _) => $"El coche cede de golpe, y {who} se hace daño."),
                new RunChoiceText("Dar la vuelta", (_, _) => "En el monte os pilla el mal tiempo.")),
            ["caja-atada"] = new RunEventText("La caja atada", "Una caja atada a un árbol, con un cartel: «COGED LO QUE NECESITÉIS». Un cable sale de ella hacia unos arbustos.",
                new RunChoiceText("Abrirla", (who, _) => $"El cable era una trampa. Salta, pero lejos: a {who} le alcanza poco. Dentro hay granadas."),
                new RunChoiceText("Dejarla", (_, _) => "Alguien la dejó para otros. O para cazarlos.")),
            ["perro"] = new RunEventText("El perro", "Un perro flaco os sigue desde hace un rato. No ladra.",
                new RunChoiceText("Darle una lata", (_, _) => "Come y echa a andar. Os lleva hasta una furgoneta volcada, con un botiquín dentro."),
                new RunChoiceText("Espantarlo", (_, _) => "Se queda mirando desde lejos.")),
            ["control"] = new RunEventText("El control", "Los Cuervos han cortado la carretera. Piden suministros por dejaros pasar.",
                new RunChoiceText("Darles lo que piden", (_, _) => "Cuentan los suministros y apartan la barrera."),
                new RunChoiceText("Pasar a la fuerza", (_, _) => "Hay tiros antes de que se retiren.")),
            ["radio"] = new RunEventText("La radio", "En un coche patrulla, la radio aún tiene batería. Por un canal, alguien pide ayuda.",
                new RunChoiceText("Contestar", (_, _) => "Os dice dónde se esconde: está en el camino."),
                new RunChoiceText("Quitar la batería", (_, _) => "La radio se calla.")),
            ["supermercado"] = new RunEventText("El supermercado", "Un supermercado con las puertas reventadas. Dentro está oscuro.",
                new RunChoiceText("Entrar todos", (_, _) => "Hay muertos entre los pasillos. Salís cargados, y con algún golpe."),
                new RunChoiceText("Mirar solo en la entrada", (_, _) => "En la entrada queda poco.")),
        };

        /// <summary>
        /// An option in short, by its button and after its line (Content.RunEvents' figures): «+25 § · La siguiente etapa, con
        /// la horda despierta», «+1 botiquín · −2 de salud a uno», «Nada».
        /// </summary>
        public static string RunEventEffects(int supplies = 0, int hurtOne = 0, int hurtAll = 0, int healAll = 0, string? gearKind = null, string? gearType = null, int gearCount = 0, string? modifier = null, bool survivor = false, bool survivorHalf = false, bool rescue = false)
        {
            var parts = new List<string>();
            if (supplies != 0) parts.Add($"{(supplies > 0 ? "+" : "−")}{Math.Abs(supplies)} §");
            if (gearKind != null && gearCount > 0) parts.Add($"+{gearCount} {(gearCount == 1 ? WordsOf(gearKind, gearType).One : WordsOf(gearKind, gearType).Many)}");
            if (healAll > 0) parts.Add($"+{healAll} de salud a todos");
            if (hurtOne > 0) parts.Add($"−{hurtOne} de salud a uno");
            if (hurtAll > 0) parts.Add($"−{hurtAll} de salud a todos");
            if (survivor) parts.Add(survivorHalf ? "Se une alguien, con la mitad de su salud" : "Se une alguien");
            if (rescue) parts.Add(RescueAhead);
            if (modifier != null) parts.Add(NextStageWith(modifier));
            return parts.Count == 0 ? "Nada" : string.Join(" · ", parts);
        }

        /// <summary>An option that marks a Rescate in the next row (Arturo, 17/11), in short.</summary>
        public const string RescueAhead = "Una de las siguientes etapas, un Rescate";

        /// <summary>An option the squad can't afford: «Faltan 20 §.», as in the Trueque.</summary>
        public static string RunEventShort(int missing)
        {
            return $"Faltan {missing} §.";
        }

        /// <summary>After the line, when what was won fits in no pack.</summary>
        public const string RunEventLost = "No cabe en ninguna mochila, y se queda aquí.";

        /// <summary>A modifier the next fight takes (Modifiers): «La siguiente etapa, de noche».</summary>
        private static string NextStageWith(string modifier)
        {
            switch (modifier)
            {
                case Modifiers.Night:
                    return "La siguiente etapa, de noche";
                case Modifiers.Storm:
                    return "La siguiente etapa, con mal tiempo";
                case Modifiers.Awake:
                    return "La siguiente etapa, con la horda despierta";
                default:
                    throw new ArgumentException($"Unknown modifier: {modifier}");
            }
        }
    }
}
