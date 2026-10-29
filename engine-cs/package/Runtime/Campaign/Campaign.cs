#nullable enable
// campaign.ts, in C# a section a file (engine-cs README): the strategy layer, a shared base run by two players at once.
//
// Co-op rules:
//  - Credits and the Bastion level are shared. Each player owns, promotes, equips
//    and recruits their own soldiers.
//  - Shared decisions (building, contacting regions, which mission to take, how to
//    answer an event) are proposals the partner accepts; a player alone in the base
//    decides directly.
//  - The Bastion's level is bought, with suministros and missions flown (nextLevel),
//    and its levels unlock technology, accessories and the base's pieces (progression.ts).
//  - Time only advances, and a mission only launches, when every connected player is ready.
//  - The story (lore.ts) opens a chapter per Bastion level; side quests (quests.ts)
//    chain operations and decisions, and rumours on the geoscape lead to some of them.
//
// The campaign is small and changes rarely, so the server broadcasts it whole after
// every command instead of streaming events like the tactical layer.
//
// Its types are in Types/Campaign.cs, CampaignCommand.cs, CampaignDefs.cs and QuestTypes.cs. Ported in parts (docs/
// UNITY.md, hito 2): this file has the exported constants and the tables of the facilities and the regions; the rules
// come in the next ones. A table computed from another (ROADS from REGIONS) goes in the same file, after it: the
// partial files' statics start in no set order.
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // --------------------------------------------------------------- facilities

        // Works are short (4–6 days): with one project at a time and ten buildings with three
        // levels, the balance simulation (2026-10-11) left most of the base unbuilt at the
        // longer times, with credits piling up.
        /// <summary>FACILITIES: its keys go in this order (Object.keys), not FacilityIds.All's.</summary>
        public static readonly IReadOnlyDictionary<string, FacilityDef> Facilities = new Dictionary<string, FacilityDef>
        {
            [FacilityIds.Workshop] = new FacilityDef
            {
                Id = FacilityIds.Workshop,
                Name = "Taller",
                Description = "Los accesorios cuestan un 25 % menos.",
                Upgrades = new[] { "Accesorios −35 %. Un torno de verdad.", "Accesorios −45 %. Teo deja de quejarse." },
                Cost = 85,
                Days = 4,
                Level = 1,
                Unique = true,
            },
            [FacilityIds.Infirmary] = new FacilityDef
            {
                Id = FacilityIds.Infirmary,
                Name = "Enfermería",
                Description = "Las heridas se curan el doble de rápido.",
                Upgrades = new[] { "Las heridas curan el triple de rápido. Agua caliente en la sala de curas.", "Las heridas curan el cuádruple de rápido. Un quirófano de verdad, con lámpara y generador propio." },
                Cost = 60,
                Days = 4,
                Level = 1,
                Unique = true,
            },
            [FacilityIds.Quarters] = new FacilityDef
            {
                Id = FacilityIds.Quarters,
                Name = "Barracón",
                Description = "+2 camas para soldados. Literas de madera y una estufa que solo se enciende de noche.",
                Upgrades = new[] { "+3 camas. Un segundo piso de literas.", "+4 camas. Mantas de verdad." },
                Cost = 40,
                Days = 2,
                Level = 1,
                Unique = false,
            },
            [FacilityIds.Training] = new FacilityDef
            {
                Id = FacilityIds.Training,
                Name = "Centro de entrenamiento",
                Description = "Los reclutas llegan con un nivel más en una habilidad. Un patio despejado y un horario.",
                Upgrades = new[] { "Los reclutas llegan con dos niveles más. Una semana de prácticas antes de salir.", "Los reclutas llegan con tres niveles más. Alguien que ya ha vuelto de fuera les enseña." },
                Cost = 140,
                Days = 6,
                Level = 2,
                Unique = true,
            },
            [FacilityIds.Range] = new FacilityDef
            {
                Id = FacilityIds.Range,
                Name = "Campo de tiro",
                Description = "+3 de puntería para todos los soldados. Dianas de cartón en una trinchera del pinar.",
                Upgrades = new[] { "+6 de puntería. Siluetas de zombi a cincuenta metros.", "+10 de puntería. Blancos móviles, con una polea de Teo." },
                Cost = 90,
                Days = 4,
                Level = 2,
                Unique = true,
            },
            [FacilityIds.Crew] = new FacilityDef
            {
                Id = FacilityIds.Crew,
                Name = "Brigada de obras",
                Description = "Obras y desbroces un 25 % más rápidos. Carretillas, poleas y manos de sobra.",
                Upgrades = new[] { "Obras y desbroces un 40 % más rápidos.", "Obras y desbroces un 50 % más rápidos." },
                Cost = 70,
                Days = 3,
                Level = 2,
                Unique = true,
            },
            [FacilityIds.Simulation] = new FacilityDef
            {
                Id = FacilityIds.Simulation,
                Name = "Sala de mapas",
                Description = "Las misiones rinden un 25 % más de suministros: se sabe dónde buscar. Un tablero con los caminos del valle.",
                Upgrades = new[] { "+40 % de suministros en las misiones. Cada casa registrada, marcada con una chincheta.", "+55 % de suministros. Las rutas de los Camioneros, y dónde paraban." },
                Cost = 100,
                Days = 5,
                Level = 3,
                Unique = true,
            },
            [FacilityIds.Garage] = new FacilityDef
            {
                Id = FacilityIds.Garage,
                Name = "Chapistería",
                Description = "Las heridas curan 1 día antes, nunca en menos de 1, y la mochila lleva un hueco más. La Lata lleva camilla.",
                Upgrades = new[] { "Las heridas curan 2 días antes. Mantas y un botiquín fijo atrás.", "Las heridas curan 3 días antes. La Lata ya no para a repostar con heridos dentro." },
                Cost = 110,
                Days = 5,
                Level = 3,
                Unique = true,
            },
            [FacilityIds.Listening] = new FacilityDef
            {
                Id = FacilityIds.Listening,
                Name = "Estación de escucha",
                Description = "Los muertos avanzan una misión más tarde y hay una misión más a la vista. Morse oye antes lo que viene.",
                Upgrades = new[] { "Los muertos, una misión más tarde.", "Los muertos, dos misiones más tarde." },
                Cost = 120,
                Days = 5,
                Level = 4,
                Unique = true,
            },
            [FacilityIds.Echoes] = new FacilityDef
            {
                Id = FacilityIds.Echoes,
                Name = "Taller de oficios",
                Description = "Lo que se aprende fuera, aquí se repasa: cada misión llena las barras un 20 % más.",
                Upgrades = new[] { "Un 40 % más. Más herramientas, y Nwosu repasa las curas.", "Un 60 % más. Gente de otros enclaves enseña lo suyo." },
                Cost = 130,
                Days = 6,
                Level = 5,
                Unique = true,
            },
            // Asked for in the second playtest: the fallen leave the Cuartel's list. Texts by Lore.
            [FacilityIds.Memorial] = new FacilityDef
            {
                Id = FacilityIds.Memorial,
                Name = "Camposanto",
                Description = "Los caídos descansan aquí y salen de la lista de Soldados; sus nombres se guardan. Cruces de pino bajo la nieve, una por cada uno.",
                Upgrades = new[]
                {
                    "+5 de voluntad para todos los soldados. Saben que, si caen, alguien los recordará.",
                    "+10 de voluntad para todos. Y quien vuelve de una misión en la que cayó alguien de la escuadra aprende un 20 % más: por ellos. Teo graba cada nombre en latón.",
                },
                Cost = 40,
                Days = 3,
                Level = 1,
                Unique = true,
            },
            // Food (docs/FASE4.md): costs and days like a barracón. Texts are placeholders for Lore.
            // The bitten's own building (playtest 2026-10-15, #13): without it none of them boards La Lata.
            [FacilityIds.Quarantine] = new FacilityDef
            {
                Id = FacilityIds.Quarantine,
                Name = "Cuarentena",
                Description = "Una nave aparte, con cerrojo por fuera. Sin ella, un mordido no sube a La Lata.",
                Upgrades = new[] { "Los mordidos tardan un día más en convertirse. Mantas, agua y alguien de guardia en la puerta.", "Los mordidos tardan dos días más en convertirse. Una estufa y una radio para las noches largas." },
                Cost = 50,
                Days = 3,
                Level = 1,
                Unique = true,
            },
            [FacilityIds.Garden] = new FacilityDef
            {
                Id = FacilityIds.Garden,
                Name = "Huerto",
                Description = "+5 raciones por misión. Bancales bajo plástico y bidones para el agua de lluvia.",
                Upgrades = new[] { "+10 raciones por misión. Un invernadero de verdad, con estufa.", "+15 raciones por misión. Riego por goteo y semillas de La Vega." },
                Cost = 40,
                Days = 2,
                Level = 1,
                Unique = true,
            },
            [FacilityIds.Relay] = new FacilityDef
            {
                Id = FacilityIds.Relay,
                Name = "Centro de comunicaciones",
                Description = "+2 zonas contactables.",
                Upgrades = new[] { "+3 zonas contactables por centro. Una antena más alta, en el pino más alto.", "+4 zonas por centro. La antena llega hasta el mar." },
                Cost = 110,
                Days = 5,
                Level = 5,
                Unique = false,
            },
        };

        /// <summary>The 8 starting soldiers sleep in the bunker; recruits need a barracón.</summary>
        public const int BunkerBeds = 8;

        // ------------------------------------------------------------------ regions

        // The valley of Ossau (docs/FASE5.md): where each zone lies on the valley's map (Arturo's, 15/10:
        // x −15…15 and z −10…10, 51,2 of its pixels a unit) and the roads between them, the same ones La Lata
        // drives. Bielsa (Atalaya's zone) is up in the north-west; the Marea comes up from Los Llanos de
        // Ceniza in the south.
        /// <summary>REGIONS: its keys in RegionIds.All's order.</summary>
        public static readonly IReadOnlyDictionary<string, RegionDef> Regions = new Dictionary<string, RegionDef>
        {
            [RegionIds.Na] = new RegionDef { Id = RegionIds.Na, Name = "Los Llanos de Ceniza", X = 2.87, Z = 6.95, Roads = new[] { RegionIds.Afr, RegionIds.Me }, Income = 35 },
            [RegionIds.Sa] = new RegionDef { Id = RegionIds.Sa, Name = "Rudnik", X = -11.48, Z = -0.98, Roads = new[] { RegionIds.Weu, RegionIds.Afr }, Income = 25 },
            [RegionIds.Weu] = new RegionDef { Id = RegionIds.Weu, Name = "Bielsa", X = -6.11, Z = -5.86, Roads = new[] { RegionIds.Oce, RegionIds.Sa, RegionIds.Eeu }, Income = 30 },
            [RegionIds.Eeu] = new RegionDef { Id = RegionIds.Eeu, Name = "Sappada", X = 12.64, Z = -8.28, Roads = new[] { RegionIds.Weu, RegionIds.Eas }, Income = 25 },
            [RegionIds.Afr] = new RegionDef { Id = RegionIds.Afr, Name = "Tarbes", X = -6.56, Z = 4.02, Roads = new[] { RegionIds.Oce, RegionIds.Na, RegionIds.Sa }, Income = 25 },
            [RegionIds.Me] = new RegionDef { Id = RegionIds.Me, Name = "Koper", X = 10.82, Z = -2.23, Roads = new[] { RegionIds.Oce, RegionIds.Na }, Income = 30 },
            [RegionIds.Eas] = new RegionDef { Id = RegionIds.Eas, Name = "El Pantano Gris", X = 3.79, Z = -6.25, Roads = new[] { RegionIds.Eeu, RegionIds.Oce }, Income = 35 },
            [RegionIds.Oce] = new RegionDef { Id = RegionIds.Oce, Name = "La Vega", X = -0.39, Z = -0.63, Roads = new[] { RegionIds.Weu, RegionIds.Eas, RegionIds.Afr, RegionIds.Me }, Income = 25 },
        };

        /// <summary>Every road once, as a pair of zones (the map draws them).</summary>
        public static readonly IReadOnlyList<IReadOnlyList<string>> Roads = RegionIds.All
            .SelectMany((a) => Regions[a].Roads.Where((b) => string.CompareOrdinal(a, b) < 0).Select((b) => (IReadOnlyList<string>)new[] { a, b }))
            .ToList();

        public const string HomeRegion = RegionIds.Weu;

        // ----------------------------------------------------------------- the Marea

        /// <summary>
        /// Zombie activity (`c.doom`) at which each zone falls under the Marea, by the
        /// road stretches between it and Los Llanos, where it comes up from (docs/FASE5.md).
        /// Bielsa never falls: at DOOM_MAX the campaign is lost.
        /// </summary>
        public static readonly IReadOnlyList<int> MareaThresholds = new[] { 4, 6, 9, 10, 11 };
        public const string MareaSource = RegionIds.Na;

        // -------------------------------------------------------------------- state

        /// <summary>RECRUIT_COST (RecruitCost is the function recruitCost, which gives it).</summary>
        public const int RecruitPrice = 30;
        public const int MaxSoldiersPerPlayer = 6;
        public const int DoomMax = 12;
        /// <summary>
        /// A mission was five days of the Bastion's clock until 21/10; now it is one (Arturo, 21/10: «la misión, 1 día», with
        /// Ana's ration a soldier a day, docs/PRUEBA-2026-10-21.md #34). What is still written in those days of before (the
        /// Taller's works, wounds, the quests' waits) goes by in missions, one for every five of them, rounded up; the clock's
        /// own intervals below are counted in the days of now.
        /// </summary>
        public const int OldDaysPerMission = 5;

        /// <summary>
        /// Campaigns from before Fase 10 (docs/FASE10.md) aren't carried over: the
        /// progression is another. The host puts such a save aside and starts anew.
        /// </summary>
        public const int FreshCampaignVersion = 20;

        /// <summary>Bumped when the campaign shape changes; older campaigns go through `migrateCampaign`.</summary>
        public const int CampaignVersion = 26;

        // ------------------------------------------------------------ infection

        /// <summary>Medicines a campaign starts with.</summary>
        public const int StartMedicines = 2;
        /// <summary>Days a bitten soldier lasts at the base before turning; the Enfermería adds INFIRMARY_TURN_DAYS.</summary>
        public const int TurnDays = 1;
        public const int InfirmaryTurnDays = 1;
        /// <summary>What curing a wound with medicines takes and gives: 1 ✚ for a day (a mission) of recovery fewer.</summary>
        public const int MedicateCost = 1;
        public const int MedicateDays = 1;
        /// <summary>The enclaves that send medicines while they are allies (contacted) and stand (no Nido in their region), 1 ✚ each every ENCLAVE_DAYS.</summary>
        public static readonly IReadOnlyList<string> Enclaves = new[] { RegionIds.Oce, RegionIds.Afr };
        public const int EnclaveDays = 1;
        /// <summary>The cause the Caídos read for a soldier who turned (placeholder for Lore).</summary>
        public const string TurnedCause = "Se convirtió";

        // ------------------------------------------------------------------- food

        /// <summary>Rations a campaign starts with (docs/FASE4.md).</summary>
        public const int StartFood = 16;
        /// <summary>The Bastion eats a ration a day for every living soldier (Ana, 21/10 #34).</summary>
        public const int EatersPerRation = 1;
        /// <summary>La Vega, an ally standing, sends 2 rations a day (besides its medicines).</summary>
        public const int LaVegaFood = 2;
        public const int LaVegaFoodDays = 1;
        /// <summary>Rations a Huerto grows a day (a mission), a level.</summary>
        public const int GardenRations = 5;
        /// <summary>Will every soldier goes on a mission without, while the Bastion is hungry.</summary>
        public const int HungryWill = 10;

        // ----------------------------------------------------------------- missions

        /// <summary>Rations and parts some operations pay (docs/FASE4.md), by seed: one in three pays rations, one in four parts.</summary>
        public const int RewardFood = 4;
        public const int RewardParts = 2;

        /// <summary>
        /// Story chapters won after which the winter is over and the missions lose
        /// their snow (playtest 16/10): three, so it ends with the sirena of chapter 3;
        /// the Marea comes with the thaw, and chapter 4 finds it out.
        /// </summary>
        public const int ThawAfterStory = 3;

        // ---------------------------------------------------------------- the sieges

        /// <summary>
        /// Missions before the first siege (docs/FASE12.md §11.5): it comes straight after the first mission, the only thing
        /// the base can do then (Ana, 22/10). Between one and the next: three, two once the Marea is half way.
        /// </summary>
        public const int SiegeFirstIn = 1;
        public const int SiegeEvery = 3;
        /// <summary>The dead in that first siege: a small one, with the base hardly built yet (Arturo, 22/10).</summary>
        public const int SiegeOpening = 6;
        /// <summary>The dead a siege is counted from after that: each one held brings SIEGE_GROWTH more, and every point of the Marea one more.</summary>
        public const int SiegeFirst = 14;
        public const int SiegeGrowth = 6;
        /// <summary>Days that go by with each mission (docs/FASE12.md §11.6): time is counted in missions, the clock inside in days.</summary>
        public const int DaysPerMission = 1;
        /// <summary>One siege in three by night (Arturo, 18/10), never the first one held: in the dark only the floodlights let the squad see far.</summary>
        public const int SiegeNight = 3;

        // ----------------------------------------------------------------- commands

        /// <summary>Soldiers La Lata takes, by vehicle (Arturo, 18/10 #45): todoterreno, furgoneta, camión, autobús.</summary>
        public static readonly IReadOnlyList<int> VehicleSeats = new[] { 4, 6, 7, 8 };
        /// <summary>Missions flown before an operation for each vehicle can turn up.</summary>
        public static readonly IReadOnlyList<int> VehicleFrom = new[] { 0, 2, 6, 10 };

        /// <summary>Soldiers a partner who joins a campaign started without them gets to command.</summary>
        public const int LateKit = 4;
    }
}
