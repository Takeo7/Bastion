#nullable enable
// quests.ts: the side quests' data and `fill` (Lore's). Its types and ids are in Types/QuestTypes.cs, with campaign.ts's.
// A few texts are fixed here and not in quests.ts, which is frozen: engine-cs/golden/text-fixes.ts says which and why.
// quests.ts's own header: side quests, the "encargos" of docs/LORE.md: chains of steps (operations or decisions taken
// with both keys) that end with a unique reward and leave a consequence behind. Data only: campaign.ts starts and runs
// them. Texts may hold placeholders in braces ({soldier}, {relative}, {a}…), filled from the quest's `names` when it
// starts.
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bastion.Engine
{
    public static class Quests
    {
        // ------------------------------------------------------------------- traits

        /// <summary>TRAITS: what a quest can leave on a soldier, for good (TraitIds).</summary>
        public static readonly IReadOnlyDictionary<string, TraitDef> Traits = new Dictionary<string, TraitDef>
        {
            ["grudge"] = new TraitDef { Name = "Rencor", Description = "+10 de puntería, −15 de voluntad. No perdona a los Cuervos.", Aim = 10, Will = -15 },
            ["scarred"] = new TraitDef { Name = "Pesadillas", Description = "−20 de voluntad. No ha vuelto a dormir una noche entera.", Aim = 0, Will = -20 },
        };

        // ------------------------------------------------------------------- quests

        /// <summary>QUESTS, in the order of QuestIds.All.</summary>
        public static readonly IReadOnlyDictionary<string, QuestDef> QuestDefs = new Dictionary<string, QuestDef>
        {
            [QuestIds.Faces] = new QuestDef
            {
                Id = QuestIds.Faces,
                Title = "Caras conocidas",
                Signature = "— N. A.",
                Hook = "{soldier} ha reconocido un nombre en la radio de los Cuervos.",
                Prize = "{relative} se une al Bastión",
                Region = null,
                Rumor = false,
                Steps = new QuestStep[]
                {
                    new QuestDecision
                    {
                        Title = "Caras conocidas",
                        Text = "Los Cuervos leen por radio su lista de prisioneros. Los cambian por comida y munición. {soldier} ha oído el nombre de {relation}, {relative}. Encontrar el campamento costará días y soldados.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Buscar el campamento", Effect = "Tres misiones", Then = "next" },
                            new QuestChoice { Label = "No podemos permitírnoslo", Effect = "{soldier} gana el rasgo «Rencor»", Then = "refused" },
                        },
                    },
                    new QuestMission
                    {
                        Name = "La lista de prisioneros",
                        Living = true,
                        Kind = "recovery",
                        Biome = "city",
                        Days = 8,
                        Briefing = "Los Cuervos llevan **un registro de prisioneros** en un control de la carretera. Ahí consta quién tiene a {relative}. ==Recuperadlo y sacadlo por la zona de evacuación.==",
                        Item = "el registro de prisioneros",
                        Outdoors = true,
                        Reward = new MissionReward { Credits = 40 },
                    },
                    new QuestMission
                    {
                        Name = "La radio del campamento",
                        Kind = "hack",
                        Biome = "city",
                        Days = 8,
                        Briefing = "El registro no dice dónde está el campamento. Su radio llega a **un repetidor roto** de la ciudad. ==Reparadlo== y lo localizaremos. Si falla, **el ruido** atraerá a lo que ande cerca.",
                        Reward = new MissionReward { Credits = 40 },
                    },
                    new QuestMission
                    {
                        Name = "Sacar a {relative}",
                        Living = true,
                        Kind = "rescue",
                        Biome = "facility",
                        Difficulty = 1,
                        Days = 8,
                        Briefing = "Los prisioneros están en **un almacén cerrado con cadenas**. {relative} está dentro. ==Hay que sacar a {relative} con vida.==",
                        Vip = "{relative}",
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["success"] = new QuestOutcome { Text = "{relative} llega al Bastión en los huesos. Al día siguiente pide un fusil.", Tone = "good", Recruit = new QuestRecruit { Name = "{relative}" }, Retry = 40 },
                    ["refused"] = new QuestOutcome { Text = "{soldier} no dice nada. Desde entonces dispara antes de preguntar.", Tone = "bad", Trait = "grudge", Retry = 30 },
                    ["failure"] = new QuestOutcome { Text = "El campamento estaba vacío cuando llegamos. {soldier} no habla de ello. Ahora dispara antes de preguntar.", Tone = "bad", Trait = "grudge", Retry = 30 },
                },
            },

            [QuestIds.Ibarra] = new QuestDef
            {
                Id = QuestIds.Ibarra,
                Title = "La comandante Lindqvist",
                Signature = "— N. A.",
                Hook = "Morse ha oído el nombre de Vera Lindqvist en una frecuencia de los Cuervos.",
                Prize = "decidir qué hacer con Lindqvist",
                Region = "weu",
                Rumor = false,
                Steps = new QuestStep[]
                {
                    new QuestMission
                    {
                        Name = "El diario del Collado",
                        Kind = "recovery",
                        Biome = "city",
                        Days = 10,
                        Briefing = "Vera Lindqvist mandaba el Refugio del Collado. El año pasado se lo entregó a los Cuervos a cambio de su vida. Por ella, aquí nadie decide solo. **Su diario** sigue en las ruinas y dice adónde fue. ==Recuperadlo y sacadlo por la zona de evacuación.==",
                        Item = "el diario",
                        Reward = new MissionReward { Credits = 50 },
                    },
                    new QuestMission
                    {
                        Name = "Vera Lindqvist",
                        Kind = "elimination",
                        Biome = "city",
                        Difficulty = 1,
                        Days = 8,
                        Briefing = "Lindqvist manda ahora **una banda de Cuervos**. ==Abatid a toda la banda.== A ella, si se puede, la quiero viva.",
                        Leader = new QuestLeader { Template = "officer", Name = "Vera Lindqvist" },
                    },
                    new QuestDecision
                    {
                        Title = "Vera Lindqvist",
                        Text = "Lindqvist está herida entre los escombros. No pide perdón. Dice que lo volvería a hacer. Sabe mandar, y sabe dónde guarda su banda lo robado.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Llevarla al Bastión", Effect = "Se une con tres barras llenas y el rasgo «Pesadillas»", Then = "captured" },
                            new QuestChoice { Label = "Acabar con esto", Effect = "+150 suministros de su alijo", Then = "killed" },
                        },
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["captured"] = new QuestOutcome { Text = "Lindqvist duerme en el Bastión, poco y con la luz encendida. Nadie le habla si no hace falta. Sabe mandar.", Tone = "good", Recruit = new QuestRecruit { Name = "Vera Lindqvist", Rank = 3, Trait = "scarred" } },
                    ["killed"] = new QuestOutcome { Text = "Lindqvist está muerta. Su alijo pasa al Bastión (+150 suministros).", Tone = "good", Credits = 150 },
                    ["failure"] = new QuestOutcome { Text = "Lindqvist ha escapado. Volverá a aparecer.", Tone = "bad", Retry = 25 },
                },
            },

            [QuestIds.Amara] = new QuestDef
            {
                Id = QuestIds.Amara,
                Title = "El ayudante de la doctora",
                Signature = "— Dra. Nwosu",
                Hook = "Alguien pregunta por radio por la Dra. Nwosu.",
                Prize = "decidir qué hacer con Tomasz Rybak",
                Region = "afr",
                Rumor = false,
                Steps = new QuestStep[]
                {
                    new QuestDecision
                    {
                        Title = "Un mensaje para la doctora",
                        Text = "Por radio, en la clave del Instituto, llega un mensaje para la Dra. Nwosu. Lo manda Tomasz Rybak, que fue su ayudante en el Instituto Koper. Ella lo daba por muerto. Trabaja para la Congregación en un laboratorio de Tarbes. Quiere hablar.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Contestar", Effect = "Una misión en el laboratorio", Then = "next" },
                            new QuestChoice { Label = "No contestar", Effect = "Sin efecto", Then = "burned" },
                        },
                    },
                    new QuestMission
                    {
                        Name = "El laboratorio de Tarbes",
                        Kind = "hack",
                        Biome = "facility",
                        Difficulty = 1,
                        Days = 8,
                        Briefing = "Rybak ha dejado abierto el laboratorio. Su ordenador va con un generador. ==Arrancadlo y leed sus notas==: sabremos si quiere salir o si es una trampa. Si falla, **la Congregación tocará las campanas** y vendrán todos.",
                    },
                    new QuestDecision
                    {
                        Title = "Tomasz Rybak",
                        Text = "Rybak quiere salir. La Congregación lo tiene estudiando cómo guiar a los zombis con sonido. A escondidas, ha avanzado con los antibióticos. Si lo sacamos, trae sus cuadernos. Si quemamos el laboratorio, la Voz pierde un año de trabajo. La Dra. Nwosu no opina.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Sacarlo de allí", Effect = "Antibióticos antes de tiempo", Then = "rescued" },
                            new QuestChoice { Label = "Quemar el laboratorio", Effect = "Los zombis retroceden (−1)", Then = "ended" },
                        },
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["rescued"] = new QuestOutcome { Text = "Rybak y la Dra. Nwosu comparten mesa y no se hablan. Los antibióticos llegan antes de tiempo.", Tone = "good", Tech = "nanoMedkit" },
                    ["ended"] = new QuestOutcome { Text = "El laboratorio arde con un año de trabajo de la Congregación dentro. Los zombis retroceden. La Dra. Nwosu pasa la noche junto a la radio.", Tone = "good", Doom = -1 },
                    ["burned"] = new QuestOutcome { Text = "Nadie contesta. La Dra. Nwosu no pregunta.", Tone = "bad" },
                    ["failure"] = new QuestOutcome { Text = "La Congregación sabe que la buscamos. Sus campanas atraen a Tarbes a los Acorazados del cuartel: uno más en cada misión de la zona.", Tone = "bad", Enemy = "mec" },
                },
            },

            [QuestIds.Bells] = new QuestDef
            {
                Id = QuestIds.Bells,
                Title = "Las campanas de Tende",
                Signature = "— Los de Tende",
                Hook = "Las campanas de Tende tocan a rebato.",
                Prize = "dos granadas aturdidoras y +10 suministros en cada convoy",
                Region = "weu",
                Rumor = true,
                Steps = new QuestStep[]
                {
                    new QuestDecision
                    {
                        Title = "Las campanas de Tende",
                        Text = "Tende es un pueblo de la sierra. Cada noche llega una horda y la gente se encierra en la iglesia. Un coche patrulla de los Cuervos, con la sirena puesta en el valle, atrae a los zombis hacia el pueblo. Cuando no quede nadie, bajarán a saquearlo.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Callar la sirena", Effect = "Una misión de sabotaje", Then = "next" },
                            new QuestChoice { Label = "Ahora no podemos", Effect = "Tende se vacía: −10 suministros en cada convoy", Then = "failure" },
                        },
                    },
                    new QuestMission
                    {
                        Name = "Las campanas de Tende",
                        Kind = "sabotage",
                        Biome = "wilds",
                        Days = 8,
                        Briefing = "**El coche patrulla** está en una loma sobre el valle, con la sirena puesta. ==Apagadla antes de que la horda llegue al pueblo.==",
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["success"] = new QuestOutcome { Text = "Tende resiste. Pagan lo pactado: las dos granadas aturdidoras del cuartelillo y parte de cada cosecha (+10 suministros en cada convoy). La puerta sigue cerrada para nosotros.", Tone = "good", Items = new Dictionary<string, int> { ["flashbang"] = 2 }, Income = 10 },
                    ["failure"] = new QuestOutcome { Text = "Las campanas de Tende ya no suenan. El pueblo está vacío (−10 suministros en cada convoy).", Tone = "bad", Income = -10 },
                },
            },

            [QuestIds.Numbers] = new QuestDef
            {
                Id = QuestIds.Numbers,
                Title = "La estación de números",
                Signature = "— N. A.",
                Hook = "Morse ha oído un indicativo que no oía desde antes de la Pandemia.",
                Prize = "una zona contactable más",
                Region = "eeu",
                Rumor = false,
                Steps = new QuestStep[]
                {
                    new QuestMission
                    {
                        Name = "La estación de números",
                        Kind = "hack",
                        Biome = "city",
                        Days = 10,
                        Briefing = "Una estación de números emite con el indicativo de Otto Brandt. Fue mi maestro. Lo creía muerto. En el tejado del ayuntamiento de Sappada hay **un repetidor roto** que recoge su señal. ==Reparadlo== y sabremos desde dónde emite. Si falla, **el ruido** atraerá a lo que ande cerca.",
                    },
                    new QuestMission
                    {
                        Name = "Otto Brandt",
                        Living = true,
                        Kind = "rescue",
                        Biome = "wilds",
                        Days = 8,
                        Briefing = "Otto está vivo. Emite desde una cabaña de Sappada con una radio de válvulas. **Los Cuervos también lo han oído.** ==Llegad antes y sacadlo con vida.==",
                        Vip = "Otto Brandt",
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["success"] = new QuestOutcome { Text = "Otto Brandt monta su radio en el Bastión. Con él, la radio llega a una zona más.", Tone = "good", Contacts = 1 },
                    ["failure"] = new QuestOutcome { Text = "No llegamos a tiempo. Morse firma el informe y no añade nada.", Tone = "bad" },
                },
            },

            [QuestIds.Route50] = new QuestDef
            {
                Id = QuestIds.Route50,
                Title = "Ruta 50",
                Signature = "— Los Camioneros",
                Hook = "Los Camioneros han perdido un convoy en los Llanos de Ceniza.",
                Prize = "+15 suministros en cada convoy",
                Region = "na",
                Rumor = true,
                Steps = new QuestStep[]
                {
                    new QuestMission
                    {
                        Name = "Ruta 50",
                        Living = true,
                        Kind = "rescue",
                        Biome = "wilds",
                        Days = 8,
                        Briefing = "Una partida de caza de los Cuervos ha asaltado el convoy en la Ruta 50. La conductora, **Lola Haskins**, se ha encerrado en una gasolinera. ==Sacadla con vida.==",
                        Vip = "Lola Haskins",
                        Reward = new MissionReward { Credits = 40 },
                    },
                    new QuestMission
                    {
                        Name = "Cacería en la Ruta 50",
                        Living = true,
                        Kind = "elimination",
                        Biome = "wilds",
                        Days = 6,
                        Briefing = "La partida que asaltó el convoy sigue en la carretera. Mientras esté ahí no pasará ningún camión. ==Abatid a toda la banda.==",
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["success"] = new QuestOutcome { Text = "Los Camioneros vuelven a la Ruta 50. Dejan su parte en el punto de cambio sin bajar del camión (+15 suministros en cada convoy).", Tone = "good", Income = 15 },
                    ["failure"] = new QuestOutcome { Text = "Los Camioneros dejan de pasar por los Llanos (−10 suministros en cada convoy).", Tone = "bad", Income = -10 },
                },
            },

            [QuestIds.Hands] = new QuestDef
            {
                Id = QuestIds.Hands,
                Title = "Manos",
                Signature = "— La Red Callada",
                Hook = "La Red Callada ha perdido a una de sus enlaces.",
                Prize = "125 suministros",
                Region = "eas",
                Rumor = true,
                Steps = new QuestStep[]
                {
                    new QuestMission
                    {
                        Name = "Manos",
                        Living = true,
                        Kind = "rescue",
                        Biome = "city",
                        Days = 8,
                        Briefing = "La Red Callada son sordos y sus familias. Han sobrevivido hablando por señas, sin un ruido. Los Cuervos se han llevado a **Mei Takahara**, una de sus enlaces. No saben lo que vale. ==Sacadla con vida.==",
                        Vip = "Mei Takahara",
                    },
                    new QuestMission
                    {
                        Name = "Las notas de Mei",
                        Living = true,
                        Kind = "recovery",
                        Biome = "city",
                        Days = 8,
                        Briefing = "Mei anotaba por dónde pasan las hordas del Pantano Gris. **Sus notas** siguen en una casa que los Cuervos están saqueando. ==Recuperadlas y sacadlas por la zona de evacuación.==",
                        Item = "las notas",
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["success"] = new QuestOutcome { Text = "La Red Callada paga el rescate de Mei (+125 suministros) y nos dice por dónde pasan las hordas del Pantano. Después, deja de contestar.", Tone = "good", Credits = 125 },
                    ["failure"] = new QuestOutcome { Text = "La Red Callada se esconde. No volveremos a saber de ella.", Tone = "bad" },
                },
            },

            [QuestIds.Mole] = new QuestDef
            {
                Id = QuestIds.Mole,
                Title = "El topo",
                Signature = "— N. A.",
                Hook = "Alguien ha saboteado el generador dos veces.",
                Prize = "encontrar al topo",
                Region = "weu",
                Rumor = false,
                Steps = new QuestStep[]
                {
                    new QuestDecision
                    {
                        Title = "El topo",
                        Text = "Alguien del Bastión pasa información a los Cuervos. Las noches de los sabotajes solo bajaron dos personas al generador: {a} y {b}. Podemos vigilar a una durante una semana.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Vigilar a {a}", Effect = "Una semana de vigilancia", Then = "next", Clue = true },
                            new QuestChoice { Label = "Vigilar a {b}", Effect = "Una semana de vigilancia", Then = "next", Clue = true },
                        },
                    },
                    new QuestDecision
                    {
                        Title = "El topo",
                        Text = "{clue} Hay que decidir antes del próximo sabotaje. Si acusamos a quien no es, el topo sabrá que lo buscamos.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Acusar a {a}", Effect = "Si es el topo, los zombis retroceden (−1)", Then = "guess" },
                            new QuestChoice { Label = "Acusar a {b}", Effect = "Si es el topo, los zombis retroceden (−1)", Then = "guess" },
                        },
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["success"] = new QuestOutcome { Text = "Confiesa {culprit}. Pasaba los turnos de guardia a los Cuervos con una radio escondida en el almacén. Sabían cuándo dormíamos y por dónde echarnos a los zombis. Sin esos avisos, los zombis retroceden.", Tone = "good", Doom = -1 },
                    ["failure"] = new QuestOutcome { Text = "Hemos acusado a quien no era. Esa noche desaparece {culprit}. Se lleva la caja (−60 suministros) y deja las obras saboteadas (4 días de retraso).", Tone = "bad", Credits = -60, Delay = 4 },
                },
                Clues = new QuestClues { Guilty = "Vigilamos a {name}, que sale de noche hacia la antena vieja y vuelve antes del cambio de guardia.", Innocent = "Vigilamos a {name}, que no se mueve de su catre en toda la semana." },
            },

            [QuestIds.Mine] = new QuestDef
            {
                Id = QuestIds.Mine,
                Title = "La mina",
                Signature = "— El Sindicato de la Mina",
                Hook = "De una mina abandonada de Rudnik salen gritos por la noche.",
                Prize = "un recluta y dos granadas",
                Region = "sa",
                Rumor = true,
                Steps = new QuestStep[]
                {
                    new QuestDecision
                    {
                        Title = "La mina",
                        Text = "Las galerías de una mina abandonada de Rudnik están llenas de zombis. Dentro hay un Chillón que los llama. Cada noche salen más hacia los pueblos.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Bajar a la mina", Effect = "Una misión contra el Chillón", Then = "next" },
                            new QuestChoice { Label = "Sellarla con dinamita", Effect = "Sin efecto", Then = "sealed" },
                        },
                    },
                    new QuestMission
                    {
                        Name = "La mina",
                        Kind = "elimination",
                        Biome = "wilds",
                        Difficulty = 1,
                        Days = 8,
                        Briefing = "**El Chillón de la mina** lleva semanas llamando a los zombis del valle. ==Abatid a todo lo que haya. Primero el Chillón.==",
                        Leader = new QuestLeader { Template = "sectoid", Name = "El Chillón de la mina" },
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["success"] = new QuestOutcome { Text = "La mina queda en silencio. El Sindicato paga con dos cargas de dinamita (+2 granadas) y con un barrenero que quiere irse de allí.", Tone = "good", Recruit = new QuestRecruit(), Items = new Dictionary<string, int> { ["fragGrenade"] = 2 } },
                    ["sealed"] = new QuestOutcome { Text = "El Sindicato sella la mina. Nadie sabe cuántos quedaron dentro.", Tone = "bad" },
                    ["failure"] = new QuestOutcome { Text = "Los mineros abandonan Rudnik (−10 suministros en cada convoy).", Tone = "bad", Income = -10 },
                },
            },

            [QuestIds.Sunken] = new QuestDef
            {
                Id = QuestIds.Sunken,
                Title = "El camión del río",
                Signature = "— Las Bodegas",
                Hook = "Los de las Bodegas venden lo que han encontrado en el río.",
                Prize = "150 suministros",
                Region = "oce",
                Rumor = true,
                Steps = new QuestStep[]
                {
                    new QuestDecision
                    {
                        Title = "El camión del río",
                        Text = "Con el río bajo ha aparecido un camión de suministros hundido junto al puente de La Vega. La carga va precintada y casi toda sigue seca. Los Cuervos también lo han visto.",
                        Options = new[]
                        {
                            new QuestChoice { Label = "Llegar antes que ellos", Effect = "Una misión de recuperación", Then = "next" },
                            new QuestChoice { Label = "Dejarlo en el río", Effect = "Sin efecto", Then = "left" },
                        },
                    },
                    new QuestMission
                    {
                        Name = "El camión del río",
                        Kind = "recovery",
                        Biome = "wilds",
                        Days = 6,
                        Briefing = "Los de las Bodegas han sacado la carga del camión y la han dejado junto a la carretera, bajo una lona: la mitad, para quien la saque antes que los Cuervos. El pueblo de al lado está lleno de muertos, y **los Cuervos vienen de camino**. ==Recuperadla y sacadla en La Lata antes de que lleguen.==",
                        Item = "la carga",
                        Outdoors = true,
                    },
                },
                Outcomes = new Dictionary<string, QuestOutcome>
                {
                    ["success"] = new QuestOutcome { Text = "La carga trae conservas, vendas y munición. La mitad es nuestra (+150 suministros).", Tone = "good", Credits = 150 },
                    ["left"] = new QuestOutcome { Text = "El camión se queda en el fondo del río.", Tone = "bad" },
                    ["failure"] = new QuestOutcome { Text = "Los Cuervos se llevan la carga.", Tone = "bad" },
                },
            },
        };

        // ------------------------------------------------------------------- people

        /// <summary>Relatives a soldier can hear named on the Cuervos' prisoner list ("Caras conocidas").</summary>
        public static readonly IReadOnlyList<Relative> Relatives = new[]
        {
            new Relative { Name = "Mariam", Relation = "su hermana", Sex = "f" },
            new Relative { Name = "Tomás", Relation = "su hermano", Sex = "m" },
            new Relative { Name = "Ewa", Relation = "su madre", Sex = "f" },
            new Relative { Name = "Andrés", Relation = "su padre", Sex = "m" },
            new Relative { Name = "Clara", Relation = "su hermana pequeña", Sex = "f" },
            new Relative { Name = "Yusuf", Relation = "su primo", Sex = "m" },
        };

        /// <summary>People with access to the generator ("El topo"); Simão joins them if he was trusted.</summary>
        public static readonly IReadOnlyList<string> Suspects = new[]
        {
            "Pavel Lanz, el electricista",
            "Rosa Alves, la cocinera",
            "Bruno Sauer, el de la radio",
        };

        /// <summary>What rumours are about ("Se habla de …").</summary>
        public static readonly IReadOnlyList<string> RumorLines = new[]
        {
            "humo en una chimenea de un pueblo vacío",
            "una radio que repite un nombre",
            "una farmacia que nadie ha saqueado",
            "una horda que baja hacia el río",
            "un convoy que no llegó",
            "luces en una mina cerrada",
            "campanas que suenan a deshora",
            "un perro que ladra en una granja abandonada",
        };

        /// <summary>"{relative} llega al Bastión…" with the quest's names; a placeholder without a name stays as it is.</summary>
        public static string Fill(string text, IReadOnlyDictionary<string, string> names)
        {
            // JavaScript's \w: ASCII letters, digits and _.
            return Regex.Replace(text, "\\{([A-Za-z0-9_]+)\\}", m => names.TryGetValue(m.Groups[1].Value, out var name) ? name : m.Value);
        }
    }
}
