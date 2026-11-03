using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------ story

        /// <summary>SimonChoice: what the commanders did when Simão knocked on the door (StoryState.simon); null, not answered yet.</summary>
        public static class SimonChoices
        {
            public const string Trusted = "trusted";
            public const string Refused = "refused";

            public static readonly IReadOnlyList<string> All = new[] { Trusted, Refused };
        }

        public sealed class StoryChapter
        {
            /// <summary>Also the mission's name.</summary>
            public string Title { get; set; } = "";
            public string Kind { get; set; } = ""; // MissionKinds
            public string Biome { get; set; } = ""; // Biomes
            public string Region { get; set; } = ""; // RegionIds
            public int Difficulty { get; set; }
            public MissionReward Reward { get; set; } = new MissionReward();
            /// <summary>What the chapter gives besides the reward, for the briefing.</summary>
            public string? Prize { get; set; }
            public string Briefing { get; set; } = "";
            public string Signature { get; set; } = "";
            /// <summary>The living hold it (docs/ZOMBIS-PRIMERO.md): bands of the Cuervos or the Congregación among the dead.</summary>
            public bool? Living { get; set; } // true
            /// <summary>Recovery: what the squad carries out.</summary>
            public string? Item { get; set; }
            /// <summary>Rescue: who waits at the objective.</summary>
            public string? Vip { get; set; }
            /// <summary>Read in the debrief after winning it.</summary>
            public string After { get; set; } = "";
        }

        /// <summary>One story mission per Bastion level, in order; the final mission needs all of them.</summary>
        public static readonly IReadOnlyList<StoryChapter> Story = new[]
        {
            new StoryChapter
            {
                Title = "El consultorio de Metsovo",
                Kind = "recovery",
                Biome = "city",
                Region = "weu",
                Difficulty = 1,
                Reward = new MissionReward { Credits = 90 },
                Briefing = "El consultorio de Metsovo, el pueblo de abajo, sigue cerrado. En su nevera quedaba **insulina**, y con este frío puede que siga buena. En el campamento hay quien no pasará el invierno sin ella.\n\nLa nieve ha sepultado a muchos caminantes, pero no sabemos cómo estará el pueblo. ==Buscad una entrada y traed la insulina.==",
                Signature = "",
                Item = "la insulina",
                After = "La insulina ya está en la autocaravana, en una caja con nieve. Hay para todo el invierno.",
            },
            new StoryChapter
            {
                Title = "La doctora",
                Kind = "rescue",
                Biome = "city",
                Region = "afr",
                Difficulty = 1,
                Reward = new MissionReward { Credits = 60 },
                Prize = "la Dra. Nwosu se une al Bastión (el botín vale un 25 % más)",
                Briefing = "Una doctora lleva una semana encerrada en **el hospital de Tarbes**, con el edificio lleno de zombis. Se llama **Amara Nwosu** y trabajaba en el Instituto Koper. ==Llegad hasta ella y sacadla con vida.==",
                Signature = "— N. A.",
                Vip = "Dra. Amara Nwosu",
                After = "La Dra. Nwosu se queda en el Bastión. Sabe qué aprovechar de cada saqueo: el botín valdrá un 25 % más.",
            },
            new StoryChapter
            {
                Title = "La sirena",
                Living = true,
                Kind = "sabotage",
                Biome = "wilds",
                Region = "na",
                Difficulty = 2,
                Reward = new MissionReward { Credits = 80, Doom = 1 },
                Briefing = "Los Cuervos tienen en los Llanos **un coche patrulla con la sirena puesta**, y la encienden cada noche: nos trae a media comarca. Quiero que se calle. ==Destruidlo antes de que termine la cuenta atrás.==",
                Signature = "— T. V.",
                After = "La sirena se calló y los zombis del valle se dispersaron en un día. Teo lo apuntó todo. Esa misma semana empezó el deshielo.",
            },
            new StoryChapter
            {
                Title = "La torre del Pico Gris",
                Kind = "hack",
                Biome = "wilds",
                Region = "eas",
                Difficulty = 2,
                Reward = new MissionReward { Credits = 120, Doom = 1 },
                Briefing = "El **repetidor del Pico Gris** lleva muerto desde el invierno. ==Arregladlo== y oiremos a los demás enclaves del valle. Si falla, **el ruido** atraerá a todo lo que ande por el pantano.",
                Signature = "— N. A.",
                After = "El repetidor vuelve a funcionar. Lo primero que hemos oído es a los demás enclaves avisando de lo mismo: la Marea.",
            },
            new StoryChapter
            {
                Title = "Los inmunes",
                Living = true,
                Kind = "rescue",
                Biome = "wilds",
                Region = "eeu",
                Difficulty = 3,
                Reward = new MissionReward { Credits = 80, Recruit = true },
                Prize = "una niña inmune a salvo en el Bastión",
                Briefing = "Simão entró en el campamento de los Cuervos a por **la niña inmune**, pero lo han descubierto. Sabe dónde la esconden. ==Llegad hasta él y sacadlo con vida.==",
                Signature = "— N. A.",
                Vip = "Simão Ferreira",
                After = "Noor está en la enfermería. Se contagió hace un mes y sigue viva. Nwosu no ha dormido.",
            },
            new StoryChapter
            {
                Title = "Los cuadernos de Koper",
                Kind = "recovery",
                Biome = "facility",
                Region = "me",
                Difficulty = 3,
                Reward = new MissionReward { Credits = 150, Doom = 1 },
                Briefing = "Los cuadernos del Instituto Koper siguen en el laboratorio donde empezó todo. **Sin ellos no habrá vacuna.** ==Recuperad los cuadernos y sacadlos por la zona de evacuación.==",
                Signature = "— T. V.",
                Item = "los cuadernos",
                After = "Los cuadernos ya están en el Bastión. Teo los ha leído enteros y lleva dos días sin hablar.",
            },
            new StoryChapter
            {
                Title = "La Voz",
                Living = true,
                Kind = "rescue",
                Biome = "city",
                Region = "na",
                Difficulty = 4,
                Reward = new MissionReward { Credits = 100 },
                Prize = "cómo parar la Marea (la misión final, con el Bastión en su último nivel)",
                Briefing = "**Celia Marsh, la Voz** de la Congregación, emite cada noche desde una vieja emisora de los Llanos. Es la única que sabe cómo parar la Marea. ==Asaltad la emisora, llegad hasta ella y sacadla con vida.==",
                Signature = "— N. A.",
                Vip = "Celia Marsh",
                After = "La Voz está en el Bastión. Ha pasado una hora callada. Luego ha dicho cómo se para la Marea.",
            },
        };

        /// <summary>Without Simão inside, «Los inmunes» is a break-in, straight for the girl.</summary>
        private static class SimonRefused
        {
            public const string Briefing = "Sin nadie dentro, habrá que **entrar a la fuerza** en el campamento de los Cuervos. La niña inmune se llama **Noor Haddad**. ==Llegad hasta ella y sacadla con vida.==";
            public const string Vip = "Noor Haddad";
        }

        public static string StoryBriefing(int chapter, string? simon)
        {
            return chapter == SimonChapter && simon == SimonChoices.Refused ? SimonRefused.Briefing : Story[chapter - 1].Briefing;
        }

        /// <summary>Who waits at the objective of a story rescue.</summary>
        public static string? StoryVip(int chapter, string? simon)
        {
            return chapter == SimonChapter && simon == SimonChoices.Refused ? SimonRefused.Vip : Story[chapter - 1].Vip;
        }
    }
}
