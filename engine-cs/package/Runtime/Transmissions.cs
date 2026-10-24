// transmissions.ts: "Transmisiones", short radio conversations between the Bastion's people at the story's turning
// points (docs/LORE.md). Text only. Each client plays a scene once, when its condition first holds, and keeps it in the
// radio archive; nothing here changes the campaign. Its types are in Types/TransmissionTypes.cs.
//
// `{saludo}` becomes "Comandantes Ana y Luis" (or "Comandante Ana" alone); `{yo}`, "Comandante Ana" for the player
// reading it.
//
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Transmissions
    {
        public static readonly IReadOnlyDictionary<string, RadioSpeaker> Speakers = new Dictionary<string, RadioSpeaker>
        {
            [SpeakerIds.Morse] = new RadioSpeaker { Name = "Nadia Albescu, «Morse»", Role = "Radio" },
            [SpeakerIds.Teo] = new RadioSpeaker { Name = "Teodor Vidić, «Teo»", Role = "Taller" },
            [SpeakerIds.Nwosu] = new RadioSpeaker { Name = "Dra. Amara Nwosu", Role = "Enfermería" },
            [SpeakerIds.Simon] = new RadioSpeaker { Name = "Simão Ferreira", Role = "Desertor de los Cuervos" },
            [SpeakerIds.Voz] = new RadioSpeaker { Name = "Celia Marsh, «la Voz»", Role = "La Congregación" },
            [SpeakerIds.Halvorsen] = new RadioSpeaker { Name = "Ruth Halvorsen", Role = "Refugio de Bielsa" },
            [SpeakerIds.Coro] = new RadioSpeaker { Name = "Voz sin nombre", Role = "Frecuencia de la Congregación" },
        };

        private static int Level(CampaignState c) => Progression.BastionLevel(c.Level);

        private static bool Trusted(CampaignState c) => c.Story.Simon == Lore.SimonChoices.Trusted;

        private static bool HasEchoes(CampaignState c) => (c.Bastion?.Pieces ?? new List<BasePiece>()).Any(p => p.Piece == "echoes");

        private static bool ChapterOpen(CampaignState c, int n) => c.Story.Done >= n || c.Offers.Any(o => o.Story == n);

        private static int Bitten(CampaignState c) => c.Soldiers.Count(s => s.Alive && s.Infection != null);

        /// <summary>shortMedicines' condition: more bitten than the medicines can treat.</summary>
        private static bool ShortOfMedicines(CampaignState c)
        {
            var sick = Bitten(c);
            return sick > 0 && (c.Medicines ?? 0) < sick * Campaign.TreatCost(c);
        }

        /// <summary>In story order: when several unlock at once they play in this order.</summary>
        public static readonly IReadOnlyList<RadioScene> Scenes = new[]
        {
            new RadioScene
            {
                Id = SceneIds.Council,
                Title = "El trato con el Refugio",
                Channel = "Onda corta · Refugio de Bielsa",
                When = c => c.Day > 31,
                Lines = new[]
                {
                    new SceneLine { Who = "halvorsen", Text = "Bastión, aquí el Refugio de Bielsa. Lo pactado está en el punto de cambio. No subáis más allá." },
                    new SceneLine { Who = "halvorsen", Text = "Cada caja sale de una despensa que también pasa hambre. Si no cumplís, se acaba el trato." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Ch1After,
                Title = "Campanas de fondo",
                Channel = "Sala de radio",
                When = c => c.Story.Done >= 1 && Level(c) >= 2,
                Lines = new[]
                {
                    new SceneLine { Who = "morse", Text = "Anoche, en onda media, una mujer hablaba de los «salvados». Había campanas de fondo." },
                    new SceneLine { Who = "teo", Text = "Apúntalo." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Ch2Before,
                Title = "Una llamada de Tarbes",
                Channel = "Onda corta · 7.040 kHz",
                When = c => ChapterOpen(c, 2),
                Lines = new[]
                {
                    new SceneLine { Who = "morse", Text = "Llamada desde el hospital de Tarbes. Una doctora, encerrada en la tercera planta desde hace una semana." },
                    new SceneLine { Who = "morse", Text = "Trabajaba en el Instituto Koper. Se le acaba el agua." },
                    new SceneLine { Who = "teo", Text = "Si sabe algo del Koper, hay que sacarla." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Ch2After,
                Title = "La doctora",
                Channel = "Línea interna · Enfermería",
                When = c => c.Story.Done >= 2,
                Lines = new[]
                {
                    new SceneLine { Who = "nwosu", Text = "Amara Nwosu. Virología, Instituto Koper." },
                    new SceneLine { Who = "nwosu", Text = "Cuando cerraron las puertas salí con mis notas y nada más. Creo que hay una vacuna." },
                    new SceneLine { Who = "nwosu", Text = "Me faltan muestras, tiempo y una nevera que no se apague." },
                    new SceneLine { Who = "teo", Text = "La nevera es cosa mía." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Ch3Before,
                Title = "La sirena",
                Channel = "Línea interna · Taller",
                When = c => ChapterOpen(c, 3),
                Lines = new[]
                {
                    new SceneLine { Who = "teo", Text = "Los Cuervos tienen un coche patrulla en los Llanos de Ceniza. Lo llevan junto a quien no les paga, encienden la sirena, y los muertos acuden." },
                    new SceneLine { Who = "teo", Text = "Así vaciaron dos aldeas el invierno pasado. Quiero que se calle." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Ch3After,
                Title = "Silencio en los Llanos",
                Channel = "Onda corta · 4.625 kHz",
                When = c => c.Story.Done >= 3,
                Lines = new[]
                {
                    new SceneLine { Who = "teo", Text = "Tres días sin una horda en la carretera." },
                    new SceneLine { Who = "teo", Text = "Los muertos no piensan: van al ruido. Quien controla el ruido, controla la horda." },
                    new SceneLine { Who = "morse", Text = "Y se va la nieve. Lo que tapó vuelve a andar." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.FirstFlood,
                Title = "Los Llanos no contestan",
                Channel = "Sala de radio",
                When = c => (c.Flooded?.Count ?? 0) > 0,
                Lines = new[]
                {
                    new SceneLine { Who = "morse", Text = "Los Llanos de Ceniza no contestan desde ayer." },
                    new SceneLine { Who = "morse", Text = "El último mensaje era un hombre contando muertos desde un tejado. Paró en el doscientos." },
                    new SceneLine { Who = "teo", Text = "De allí ya no va a llegar nada." },
                    new SceneLine { Who = "morse", Text = "Y lo que baja de los Llanos sube por las carreteras. Si la infestación sigue creciendo, las siguientes serán Tarbes y Koper." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Partitura,
                Title = "La Marea",
                Channel = "Sala de radio",
                When = c => Level(c) >= 4,
                Lines = new[]
                {
                    new SceneLine { Who = "morse", Text = "Con la antena nueva oigo a los otros enclaves. Todos dicen lo mismo: con el deshielo sube algo desde los Llanos de Ceniza, miles de muertos que no se dispersan. La llaman la Marea." },
                    new SceneLine { Who = "teo", Text = "Y el Bastión está al final de la cuesta." },
                    new SceneLine { Who = "morse", Text = "Desde hoy, el reloj cuenta kilómetros." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Simon,
                Title = "Alguien en la puerta",
                Channel = "Entrada del claro",
                When = c => c.Event == "simon" || c.Story.Simon != null,
                Lines = new[]
                {
                    new SceneLine { Who = "simon", Text = "Me llamo Simão Ferreira. Hasta hace un mes iba con los Cuervos." },
                    new SceneLine { Who = "morse", Text = "Hace un mes nos habrías vendido." },
                    new SceneLine { Who = "simon", Text = "Sí. Ya no. Sé dónde tienen a una niña a la que muerden y no se levanta." },
                    new SceneLine { Who = "nwosu", Text = "Si es verdad, esa niña vale más que todo el valle." },
                    new SceneLine { Who = "morse", Text = "Decidid vosotros. Yo no le abriría." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Ch5After,
                Title = "La niña",
                Channel = "Línea interna · Enfermería",
                When = c => c.Story.Done >= 5,
                Lines = new[]
                {
                    new SceneLine { Who = "nwosu", Text = "Está a salvo. Tres mordiscos viejos en el brazo y ni una décima de fiebre." },
                    new SceneLine { Who = "simon", Text = "Os dije dónde estaba.", If = Trusted },
                    new SceneLine { Who = "nwosu", Text = "El virus entra en su sangre y no encuentra dónde agarrarse. Es el principio de una vacuna." },
                    new SceneLine { Who = "teo", Text = "Pregunta cuándo puede subir a La Lata." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Velmar,
                Title = "Taller de oficios",
                Channel = "Taller de oficios",
                When = HasEchoes,
                Lines = new[]
                {
                    new SceneLine { Who = "teo", Text = "El Taller de oficios está abierto. Lo que se aprende fuera, aquí se repasa con calma." },
                    new SceneLine { Who = "nwosu", Text = "Las curas las repaso yo." },
                },
            },
            // Phases 3 and 4 (docs/FASE3.md, FASE4.md): the bite, medicines, food and found weapons.
            new RadioScene
            {
                Id = SceneIds.FirstBite,
                Title = "Fiebre",
                Channel = "Línea interna · Enfermería",
                When = c => Bitten(c) > 0,
                Lines = new[]
                {
                    new SceneLine { Who = "nwosu", Text = "Treinta y nueve y subiendo. Es el mordisco. Está en la Cuarentena, con el cerrojo echado." },
                    new SceneLine { Who = "nwosu", Text = "Un día, alguno más con la Enfermería o con la Cuarentena mejor montada. Con el tratamiento, la fiebre baja." },
                    new SceneLine { Who = "nwosu", Text = "Cada tratamiento se lleva medicinas, y no nos sobran." },
                    new SceneLine { Who = "morse", Text = "Con fiebre no sale de misión. El tratamiento lo aprobáis los dos." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.ShortMedicines,
                Title = "No hay para todos",
                Channel = "Línea interna · Enfermería",
                When = ShortOfMedicines,
                Lines = new[]
                {
                    new SceneLine { Who = "nwosu", Text = "No hay medicinas para todos. Lo he contado tres veces." },
                    new SceneLine { Who = "nwosu", Text = "A quién se le dan no lo decido yo." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.FirstTurned,
                Title = "La fiebre no bajó",
                Channel = "Sala de radio",
                When = c => c.Soldiers.Any(s => !s.Alive && s.FellCause == "turned"),
                Lines = new[]
                {
                    new SceneLine { Who = "morse", Text = "Se ha ido esta mañana. La fiebre no bajó." },
                    new SceneLine { Who = "teo", Text = "Me he encargado yo." },
                    new SceneLine { Who = "morse", Text = "Apuntad su nombre." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Hunger,
                Title = "La despensa",
                Channel = "Línea interna · Taller",
                When = c => c.Hungry == true,
                Lines = new[]
                {
                    new SceneLine { Who = "teo", Text = "Se ha acabado la despensa." },
                    new SceneLine { Who = "morse", Text = "Con hambre, en las misiones se aguanta peor el miedo, y las heridas tardan el doble en cerrar." },
                    new SceneLine { Who = "teo", Text = "Hacen falta raciones: despensas, tiendas, almacenes. Y un Huerto, si queda tierra." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.FoundWeapon,
                Title = "En una mochila",
                Channel = "Línea interna · Armería",
                When = c => (c.FoundWeapons ?? new List<FoundWeapon>()).Any(w => Content.FoundWeapons[w.Kind].Better),
                Lines = new[]
                {
                    new SceneLine { Who = "teo", Text = "Ha llegado un arma buena en una mochila. Está limpia y engrasada." },
                    new SceneLine { Who = "teo", Text = "Está en la Armería. Decidme quién la lleva. Si cae, se pierde." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Confession,
                Title = "La noche de Koper",
                Channel = "Sala de radio · 03:10",
                When = c => Level(c) >= 6,
                Lines = new[]
                {
                    new SceneLine { Who = "morse", Text = "Teo. Nwosu dice que en el Koper había un vigilante de noche. El último que vio el sótano." },
                    new SceneLine { Who = "teo", Text = "Era yo." },
                    new SceneLine { Who = "teo", Text = "Hacía las noches para estirar la pensión. A las tres y diez saltó una alarma en el sótano. Saltaba cada dos por tres. La apagué." },
                    new SceneLine { Who = "teo", Text = "Tres semanas después, los muertos andaban por Tarbes." },
                    new SceneLine { Who = "morse", Text = "Dos inviernos callado." },
                    new SceneLine { Who = "teo", Text = "Dos inviernos intentando arreglarlo." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Ch6After,
                Title = "Los cuadernos de Koper",
                Channel = "Sala de radio",
                When = c => c.Story.Done >= 6,
                Lines = new[]
                {
                    new SceneLine { Who = "nwosu", Text = "Están todos: pruebas, muestras, fallos. Estábamos más cerca de lo que yo creía." },
                    new SceneLine { Who = "teo", Text = "Y el parte de aquella noche. «03:10, alarma en el sótano. Apagada por el vigilante.»" },
                    new SceneLine { Who = "nwosu", Text = "Esa alarma saltaba tres veces por semana, Teo. También lo pone aquí." },
                    new SceneLine { Who = "morse", Text = "Alguien ha entrado en nuestra frecuencia." },
                    new SceneLine { Who = "coro", Text = "Los de Atalaya. Quemad esos cuadernos. Nadie está enfermo. Solo os falta dar el último paso." },
                    new SceneLine { Who = "morse", Text = "La Congregación. Saben lo que hemos traído." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Contranota,
                Title = "La emisora",
                Channel = "Línea interna · Taller",
                When = c => Level(c) >= 7,
                Lines = new[]
                {
                    new SceneLine { Who = "nwosu", Text = "La vacuna está a semanas. Si la Marea nos deja esas semanas." },
                    new SceneLine { Who = "morse", Text = "No nos las va a dejar. Cada noche la Voz habla por radio, y la Marea gira hacia donde ella dice." },
                    new SceneLine { Who = "teo", Text = "Campanas y altavoces. La lleva como a un rebaño." },
                    new SceneLine { Who = "morse", Text = "Es Celia Marsh. Hacía el programa de la tarde en Radio Tarbes." },
                    new SceneLine { Who = "teo", Text = "Hay que sacarla de su emisora. Viva: solo ella sabe cómo se deshace la Marea." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Voz,
                Title = "Fuera de antena",
                Channel = "El Bastión · bajo llave",
                When = c => c.Story.Done >= 7,
                Lines = new[]
                {
                    new SceneLine { Who = "voz", Text = "Buenas noches, Ossau. Están escuchando a la Voz." },
                    new SceneLine { Who = "morse", Text = "Ya no está en antena. Aquí solo la oímos nosotros." },
                    new SceneLine { Who = "voz", Text = "Qué silencio tienen aquí arriba." },
                    new SceneLine { Who = "voz", Text = "La Marea no me obedece a mí. Obedece al ruido más fuerte. Mis campanas solo le enseñaban el camino." },
                    new SceneLine { Who = "voz", Text = "Denle un ruido más fuerte que mi voz, lejos de los vivos, y no lo apaguen hasta que se gire la última cabeza." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.Eve,
                Title = "La víspera",
                Channel = "Línea interna · todas las salas",
                When = c => c.Offers.Any(o => o.Final) || c.Event == "finale" || c.Ending != null,
                Lines = new[]
                {
                    new SceneLine { Who = "morse", Text = "La Marea se ve desde lo alto de la cuesta. Llena el valle." },
                    new SceneLine { Who = "teo", Text = "La Lata tiene gasolina para un viaje y la sirena de los Cuervos atornillada al techo." },
                    new SceneLine { Who = "nwosu", Text = "La vacuna está lista. Pocas dosis. Habrá que decidir para quién." },
                    new SceneLine { Who = "simon", Text = "Los Cuervos no se la merecen. Aunque mi hermano siga con ellos.", If = Trusted },
                    new SceneLine { Who = "morse", Text = "Lo decidiréis los dos." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.EndingBreak,
                Title = "Para todos",
                Channel = "Onda corta · todas las frecuencias",
                When = c => c.Ending == "break",
                Lines = new[]
                {
                    new SceneLine { Who = "morse", Text = "Aquí el Bastión, a cualquiera con una radio encendida. Coged papel: así se hace la vacuna." },
                    new SceneLine { Who = "nwosu", Text = "La receta entera, en abierto. Para los enclaves y para los Cuervos." },
                    new SceneLine { Who = "teo", Text = "Alguno la venderá. Otro la usará para mandar." },
                    new SceneLine { Who = "morse", Text = "Contestan sitios que no sabía que seguían vivos. Doce frecuencias. Trece." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.EndingRecord,
                Title = "Para quienes cumplieron",
                Channel = "Onda corta",
                When = c => c.Ending == "record",
                Lines = new[]
                {
                    new SceneLine { Who = "teo", Text = "Última caja de dosis. Sale mañana en La Lata, para las Bodegas." },
                    new SceneLine { Who = "morse", Text = "¿Y la receta?" },
                    new SceneLine { Who = "nwosu", Text = "Aquí, bajo llave." },
                    new SceneLine { Who = "morse", Text = "Anoche llamaron desde el otro lado de la sierra. Preguntaban por una vacuna." },
                    new SceneLine { Who = "teo", Text = "¿Qué les dijiste?" },
                    new SceneLine { Who = "morse", Text = "Que aquí no sabemos nada. El Bastión, cambio y corto." },
                },
            },
            new RadioScene
            {
                Id = SceneIds.GranCoral,
                Title = "Kilómetro cero",
                Channel = "Todas las frecuencias",
                When = c => c.Outcome == "defeat" && c.Doom >= Campaign.DoomMax,
                Lines = new[]
                {
                    new SceneLine { Who = "coro", Text = "Kilómetro cero. Las campanas suenan en la sierra." },
                    new SceneLine { Who = "coro", Text = "Abrid las puertas. Todos seréis salvados." },
                    new SceneLine { Who = "morse", Text = "Aquí el Bastión. Si alguien recibe esto: lo intentamos." },
                },
            },
        };

        /// <summary>Scenes whose moment has come, in story order.</summary>
        public static List<RadioScene> UnlockedScenes(CampaignState c)
        {
            return Scenes.Where(s => s.When(c)).ToList();
        }

        /// <summary>Scenes tied to what is on screen (Simão at the door, the end): they play as soon as they unlock.</summary>
        private static readonly IReadOnlyList<string> Urgent = new[] { SceneIds.Simon, SceneIds.EndingBreak, SceneIds.EndingRecord, SceneIds.GranCoral };

        /// <summary>
        /// What to play now among the unlocked scenes not seen yet: an urgent one at
        /// any time; otherwise the first in story order, and only at a `moment` (back
        /// at the base, or after a scan), so they never pile up.
        /// </summary>
        public static RadioScene? NextScene(CampaignState c, IReadOnlyCollection<string> seen, bool moment)
        {
            var fresh = UnlockedScenes(c).Where(s => !seen.Contains(s.Id)).ToList();
            return fresh.FirstOrDefault(s => Urgent.Contains(s.Id)) ?? (moment ? fresh.FirstOrDefault() : null);
        }

        /// <summary>The lines of a scene that apply to this campaign.</summary>
        public static List<SceneLine> SceneLines(RadioScene scene, CampaignState c)
        {
            return scene.Lines.Where(l => l.If == null || l.If(c)).ToList();
        }

        // ---------------------------------------------------------------- recap

        /// <summary>Played to whoever opens a campaign already under way (a partner arriving late, another browser).</summary>
        public static readonly RadioScene Recap = new RadioScene { Id = SceneIds.Recap, Title = "Relevo", Channel = "Línea interna · El Bastión", When = c => false, Lines = new SceneLine[0] };

        /// <summary>How far the campaign has gone, in missions (never days): «llevamos 7 salidas», «aún no hemos salido».</summary>
        private static string Outings(int n)
        {
            if (n <= 0) return "aún no hemos salido";
            return n == 1 ? "llevamos una salida" : $"llevamos {n} salidas";
        }

        /// <summary>Where the campaign stands, for `slot`: the day, the Bastion, the Marea, their soldiers and the first thing to do.</summary>
        public static List<SceneLine> RecapLines(CampaignState c, int slot)
        {
            var level = Level(c);
            var story = c.Story.Done != 0 ? $"Llevamos {c.Story.Done} de {Lore.Story.Count} hojas del diario." : "El diario está en la primera hoja.";
            var mine = c.Soldiers.Where(s => s.Owner == slot && s.Alive).Select(s => s.Name).ToList();
            var names = mine.Count > 1 ? $"{string.Join(", ", mine.Take(mine.Count - 1))} y {mine[mine.Count - 1]}" : mine.Count == 1 ? mine[0] : "nadie todavía";
            var first = Campaign.NextSteps(c, slot).FirstOrDefault();
            var lines = new List<SceneLine>
            {
                new SceneLine { Who = SpeakerIds.Morse, Text = $"{{yo}}: {Outings(c.MissionsFlown ?? 0)}. El Bastión es {Lore.BastionLevelA(level)}." },
                new SceneLine { Who = SpeakerIds.Morse, Text = $"{Lore.DoomStatus(level, c.Doom)}. {story}" },
                new SceneLine { Who = SpeakerIds.Teo, Text = $"A tu mando: {names}." },
            };
            if (first != null) lines.Add(new SceneLine { Who = SpeakerIds.Morse, Text = UrgentLine(c, first.Label) });
            return lines;
        }

        /// <summary>
        /// The recap's last line, the first thing to do. Fixed in C# only (Gameplay, 24/10; text-fixes.ts says so too): the
        /// 0.10.1 lowered the step's first letter, a soldier's name too («hiro Sato se convierte…»), and a step with its own
        /// «:» came out with two, and one that ended in a full stop got a second. Now a step that starts with a soldier's name keeps
        /// it, one with its «:» goes apart, and none ends in «..».
        /// </summary>
        private static string UrgentLine(CampaignState c, string step)
        {
            // A step that ends its own sentence gets no second full stop («…levantad un Huerto..» in the 0.10.1).
            var end = step.EndsWith(".") ? "" : ".";
            if (step.Contains(": ")) return $"Lo más urgente. {step}{end}";
            if (c.Soldiers.Any(s => s.Name.Length > 0 && step.StartsWith(s.Name, System.StringComparison.Ordinal))) return $"Lo más urgente: {step}{end}";
            return $"Lo más urgente: {step.Substring(0, 1).ToLowerInvariant()}{step.Substring(1)}{end}";
        }
    }
}
