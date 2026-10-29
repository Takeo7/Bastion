#nullable enable
// board.ts: Fase 10, the hex board (docs/FASE10.md §6, Arturo 16/10): 169 hexagonal
// cells, the core in the middle and 168 skills around it in seven rings, six
// sectors of 28, one for each experience line. The core starts unlocked; a
// skill can be bought, with a point, only beside one already unlocked
// (skills.ts unlockBlocker).
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>BoardCell.kind: `'core' | 'stat' | 'passive' | 'active'`.</summary>
    public static class CellKinds
    {
        public const string Core = "core";
        public const string Stat = "stat";
        public const string Passive = "passive";
        public const string Active = "active";

        public static readonly IReadOnlyList<string> All = new[] { Core, Stat, Passive, Active };
    }

    public sealed class BoardCell
    {
        /// <summary>A cell's id: 'core' or its skill's.</summary>
        public string Id { get; set; } = "";
        /// <summary>Axial hex coordinates (q, r); the core is (0, 0).</summary>
        public int Q { get; set; }
        public int R { get; set; }
        /// <summary>0 for the core, 1 to 7 outwards.</summary>
        public int Ring { get; set; }
        /// <summary>0 to 5, SECTOR_LINES' index; -1 for the core.</summary>
        public int Sector { get; set; }
        public string? Line { get; set; } // SkillIds
        public string Kind { get; set; } = ""; // CellKinds
        public string Name { get; set; } = "";
        /// <summary>What it does, in the game's words.</summary>
        public string Effect { get; set; } = "";
        /// <summary>`Partial&lt;UnitMods&gt;`: the fields it doesn't name are 0 (Content.AddMods).</summary>
        public UnitMods? Mods { get; set; }
        /// <summary>Health and armour more.</summary>
        public int? Hp { get; set; }
        public int? Armor { get; set; }
        /// <summary>A behaviour the rules know by its perk (and the ability it gives, if any).</summary>
        public string? Perk { get; set; } // PerkIds
        public string? Ability { get; set; } // AbilityIds
        /// <summary>Uses per mission.</summary>
        public IReadOnlyDictionary<string, int>? Charges { get; set; } // ChargeIds → uses (Partial<Record>)
        /// <summary>One more use of what it already carries (nothing if it carries none).</summary>
        public IReadOnlyDictionary<string, int>? Spare { get; set; } // ChargeIds → uses (Partial<Record>)

        internal BoardCell Placed(int q, int r, int ring, int sector, string line)
        {
            return new BoardCell { Id = Id, Q = q, R = r, Ring = ring, Sector = sector, Line = line, Kind = Kind, Name = Name, Effect = Effect, Mods = Mods, Hp = Hp, Armor = Armor, Perk = Perk, Ability = Ability, Charges = Charges, Spare = Spare };
        }
    }

    public static class Board
    {
        /// <summary>A cell's id: 'core' or its skill's.</summary>
        public const string Core = "core";
        public const int BoardRings = 7;

        /// <summary>The lines in the order of their sectors round the core (skills.ts SKILL_IDS).</summary>
        public static readonly IReadOnlyList<string> SectorLines = new[] { SkillIds.Shooting, SkillIds.Melee, SkillIds.Throwing, SkillIds.Medicine, SkillIds.Searching, SkillIds.Fitness };

        public static readonly IReadOnlyDictionary<string, string> CellKindNames = new Dictionary<string, string>
        {
            [CellKinds.Core] = "Núcleo",
            [CellKinds.Stat] = "Mejora",
            [CellKinds.Passive] = "Pasiva",
            [CellKinds.Active] = "Activa",
        };

        private static Dictionary<string, int> Uses(string charge, int n)
        {
            return new Dictionary<string, int> { [charge] = n };
        }

        private static BoardCell Stat(string id, string name, string effect, UnitMods? mods = null, int? hp = null, int? armor = null, IReadOnlyDictionary<string, int>? charges = null, IReadOnlyDictionary<string, int>? spare = null)
        {
            return new BoardCell { Id = id, Kind = CellKinds.Stat, Name = name, Effect = effect, Mods = mods, Hp = hp, Armor = armor, Charges = charges, Spare = spare };
        }

        private static BoardCell Passive(string id, string name, string effect, string? perk = null, UnitMods? mods = null)
        {
            return new BoardCell { Id = id, Kind = CellKinds.Passive, Name = name, Effect = effect, Perk = perk, Mods = mods };
        }

        private static BoardCell Active(string id, string name, string effect, string perk, string ability, IReadOnlyDictionary<string, int>? charges = null, UnitMods? mods = null)
        {
            return new BoardCell { Id = id, Kind = CellKinds.Active, Name = name, Effect = effect, Perk = perk, Ability = ability, Charges = charges, Mods = mods };
        }

        /// <summary>
        /// Each sector's 28 skills, ring by ring outwards (1 in the first ring, 2 in
        /// the second… 7 in the seventh); in each ring the first lies on the sector's
        /// spoke and the rest run towards the next sector. Names and texts, Lore's to
        /// polish; the numbers, to be tuned playing.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<BoardCell>> Sectors = new Dictionary<string, IReadOnlyList<BoardCell>>
        {
            [SkillIds.Shooting] = new[]
            {
                Stat("steadyHand", "Pulso firme", "+3 de puntería con armas de fuego.", mods: new UnitMods { GunAim = 3 }),
                Stat("lightTrigger", "Dedo ligero", "+5 de crítico con armas de fuego.", mods: new UnitMods { GunCrit = 5 }),
                Stat("sidearm", "Pistolero", "+5 de puntería con la pistola.", mods: new UnitMods { PistolAim = 5 }),
                Active("aimedShot", "A la cabeza", "Un disparo con −25 de puntería que hace la mitad más de daño. Enfriamiento 3.", PerkIds.AimedShot, AbilityIds.AimedShot),
                Stat("closeIn", "A quemarropa", "+10 de puntería a 4 casillas o menos.", mods: new UnitMods { CloseRangeAim = 10 }),
                Stat("fineTrigger", "Gatillo fino", "+10 de crítico con la pistola.", mods: new UnitMods { PistolCrit = 10 }),
                Active("rapidFire", "Ráfaga", "Dos disparos seguidos, cada uno con −15 de puntería. Gasta dos balas. Enfriamiento 3.", PerkIds.RapidFire, AbilityIds.RapidFire),
                Stat("flanker", "Por el costado", "+1 de daño a los enemigos flanqueados.", mods: new UnitMods { FlankDamage = 1 }),
                Stat("rooftop", "Desde el tejado", "+10 de puntería disparando desde más alto.", mods: new UnitMods { HeightAim = 10 }),
                Stat("coldBlood", "Sangre fría", "Los disparos de reacción pierden la tercera parte de puntería que antes.", mods: new UnitMods { ReactionKeep = 20 }),
                Active("chainShot", "Disparo en cadena", "Un disparo con −15 de puntería; si da, otro. Enfriamiento 3.", PerkIds.ChainShot, AbilityIds.ChainShot),
                Stat("hollowPoint", "Punta hueca", "+1 de daño en los críticos.", mods: new UnitMods { CritDamage = 1 }),
                Stat("bigBore", "Calibre gordo", "+1 de daño con la pistola.", mods: new UnitMods { PistolDamage = 1 }),
                Passive("sentryCrit", "Vigía certero", "Los disparos de reacción también pueden ser críticos.", perk: PerkIds.GuardCrit),
                Passive("deathFromAbove", "Sin bajar", "Abatir con un arma de fuego desde más alto devuelve una acción, una vez por turno.", perk: PerkIds.DeathFromAbove),
                Active("lightningHands", "Manos rápidas", "Sin gastar acción, un disparo de pistola. Enfriamiento 4.", PerkIds.LightningHands, AbilityIds.LightningHands),
                Stat("apRounds", "Al hueco", "Los disparos ignoran 1 de blindaje.", mods: new UnitMods { Pierce = 1 }),
                Stat("shredder", "Desgarro", "Cada impacto quita 1 de blindaje más.", mods: new UnitMods { Shred = 1 }),
                Passive("serenity", "Sin roces", "Con armas de fuego, un impacto nunca se queda en roce.", perk: PerkIds.Serenity),
                Passive("coveringFire", "Fuego de cobertura", "En vigilancia, dispara también a quien ataque a la vista.", perk: PerkIds.CoveringFire),
                Stat("noEscape", "Sin escapatoria", "+10 de crítico a 4 casillas o menos.", mods: new UnitMods { CloseRangeCrit = 10 }),
                Active("killZone", "Nadie pasa", "Vigilancia que dispara a cada enemigo que se mueva a la vista, una vez a cada uno. Enfriamiento 4.", PerkIds.KillZone, AbilityIds.KillZone),
                Active("faceoff", "Cara a cara", "Un disparo de pistola a cada enemigo a la vista. Enfriamiento 4.", PerkIds.Faceoff, AbilityIds.Faceoff),
                Active("rupture", "Herida abierta", "Un disparo con +3 de daño; el objetivo recibe 3 más de cada ataque. Enfriamiento 4.", PerkIds.Rupture, AbilityIds.Rupture),
                Active("suppression", "Supresión", "Inmoviliza al objetivo: −50 de puntería hasta tu turno, y si se mueve, le disparas. Gasta dos balas.", PerkIds.Suppression, AbilityIds.Suppression),
                Passive("marker", "Marcar", "Lo que alcanza con el arma principal queda marcado: +15 de puntería para todos contra él.", perk: PerkIds.HoloTargeting),
                Stat("heavyHand", "Mano dura", "+1 de daño con el arma principal.", mods: new UnitMods { Damage = 1 }),
                Stat("eagleEye", "Ojo de águila", "+8 de puntería y +5 de crítico con armas de fuego.", mods: new UnitMods { GunAim = 8, GunCrit = 5 }),
            },
            [SkillIds.Melee] = new[]
            {
                Stat("grip", "Buen agarre", "+3 de puntería cuerpo a cuerpo.", mods: new UnitMods { MeleeAim = 3 }),
                Stat("edge", "Filo", "+5 de crítico cuerpo a cuerpo.", mods: new UnitMods { MeleeCrit = 5 }),
                Stat("sidestep", "Paso al lado", "+5 de esquiva.", mods: new UnitMods { Dodge = 5 }),
                Active("elbowRoom", "Hacerse sitio", "Un Tajo a cada enemigo que tenga al lado. Enfriamiento 3.", PerkIds.ElbowRoom, AbilityIds.ElbowRoom),
                Stat("bladeCrit", "Golpe certero", "+10 de crítico cuerpo a cuerpo.", mods: new UnitMods { MeleeCrit = 10 }),
                Stat("closeRanks", "Cerrar filas", "+5 de defensa.", mods: new UnitMods { Defense = 5 }),
                Passive("implacable", "Uno tras otro", "Abatir con el Tajo devuelve una acción, una vez por turno.", perk: PerkIds.Implacable),
                Stat("heavyBlow", "Golpe pesado", "+1 de daño cuerpo a cuerpo.", mods: new UnitMods { MeleeDamage = 1 }),
                Stat("parry", "Ir agachado", "Los disparos de reacción contra él, −10 de puntería.", mods: new UnitMods { ReactionDefense = 10 }),
                Stat("grit", "Aguante", "+10 de voluntad.", mods: new UnitMods { Will = 10 }),
                Passive("bladeMaster", "Mano de machete", "Con el machete, el Tajo hace +2 de daño y gana +15 de puntería.", perk: PerkIds.BladeMaster),
                Passive("earBlow", "Golpe al oído", "Si el Tajo da, desorienta.", mods: new UnitMods { SlashDisorient = 1 }),
                Stat("butcher", "Matarife", "+8 de puntería cuerpo a cuerpo.", mods: new UnitMods { MeleeAim = 8 }),
                Passive("bladeWall", "Nadie se acerca", "La vigilancia cuerpo a cuerpo golpea a cada enemigo que se le ponga al lado, una vez a cada uno.", perk: PerkIds.BladeWall),
                Stat("backstab", "Por la espalda", "+15 de crítico cuerpo a cuerpo.", mods: new UnitMods { MeleeCrit = 15 }),
                Passive("steelNerves", "Nervios de acero", "El grito no le hace entrar en pánico.", mods: new UnitMods { PsiGuard = 1 }),
                Stat("cleanCut", "Tajo limpio", "+1 de daño en los críticos.", mods: new UnitMods { CritDamage = 1 }),
                Stat("waist", "Cintura", "+10 de esquiva.", mods: new UnitMods { Dodge = 10 }),
                Stat("onGuard", "En guardia", "+10 de defensa en vigilancia.", mods: new UnitMods { WatchDefense = 10 }),
                Stat("hardHead", "Cabeza dura", "+1 de salud.", hp: 1),
                Stat("sweep", "Medir la distancia", "+10 de puntería cuerpo a cuerpo.", mods: new UnitMods { MeleeAim = 10 }),
                Passive("secondWind", "Segundo aliento", "Abatir cuerpo a cuerpo cura 1 de salud.", perk: PerkIds.SecondWind),
                Stat("cleaver", "Hachazo", "+2 de daño cuerpo a cuerpo.", mods: new UnitMods { MeleeDamage = 2 }),
                Stat("slippery", "Escurridizo", "+15 de esquiva.", mods: new UnitMods { Dodge = 15 }),
                Stat("duelist", "Uno contra uno", "+10 de crítico y +5 de puntería cuerpo a cuerpo.", mods: new UnitMods { MeleeCrit = 10, MeleeAim = 5 }),
                Stat("rampart", "Muralla", "+1 de blindaje.", armor: 1),
                Stat("bladeDance", "Guardia alta", "+10 de defensa.", mods: new UnitMods { Defense = 10 }),
                Stat("killerInstinct", "Sin dudar", "+15 de puntería cuerpo a cuerpo.", mods: new UnitMods { MeleeAim = 15 }),
            },
            [SkillIds.Throwing] = new[]
            {
                // The stone, from the first cell of the line and no longer everyone's (Ana's notes, 29/10: «muy op»); its id kept,
                // so whoever had bought Brazo suelto has it.
                Active("looseArm", "Piedras", "Lanza piedras: suenan donde caen y los zombis van a mirar. Si dan a alguien, 1 de daño. +1 casilla de alcance al lanzar.", PerkIds.Stones, AbilityIds.Stone, mods: new UnitMods { GrenadeRange = 1 }),
                Stat("sharpStone", "Canto afilado", "La piedra hace +1 de daño.", mods: new UnitMods { StoneDamage = 1 }),
                Stat("curtain", "Cortina", "Un humo más, si lleva humo.", spare: Uses(ChargeIds.Smoke, 1)),
                Passive("salvo", "Sin pausa", "Lanzar una granada no acaba el turno.", perk: PerkIds.Salvo),
                Stat("bigCharge", "Carga gorda", "Los explosivos hacen +1 de daño.", mods: new UnitMods { GrenadeDamage = 1 }),
                Stat("decoy", "Señuelo", "La piedra se oye 4 casillas más lejos.", mods: new UnitMods { StoneNoise = 4 }),
                Passive("goodArm", "Buen brazo", "+3 casillas de alcance al lanzar, y una granada más en cada accesorio de granadas.", perk: PerkIds.GoodArm, mods: new UnitMods { GrenadeRange = 3 }),
                Stat("shrapnel", "Metralla", "Los explosivos quitan 1 de blindaje más.", mods: new UnitMods { GrenadeShred = 1 }),
                Stat("dazzle", "Deslumbrar", "Una aturdidora más, si lleva aturdidoras.", spare: Uses(ChargeIds.Flashbang, 1)),
                Stat("overhand", "Por encima", "+2 casillas de alcance al lanzar.", mods: new UnitMods { GrenadeRange = 2 }),
                Passive("safeBlast", "Con cuidado", "Sus granadas no hacen daño a los compañeros.", perk: PerkIds.SafeBlast),
                Stat("blastWave", "Esquirlas", "El radio de los explosivos crece una casilla.", mods: new UnitMods { GrenadeRadius = 1 }),
                Stat("bandolier", "Bandolera", "Una granada más, si lleva granadas.", spare: Uses(ChargeIds.Grenade, 1)),
                Stat("boulder", "Pedrusco", "La piedra hace +1 de daño.", mods: new UnitMods { StoneDamage = 1 }),
                Stat("demolition", "Demolición", "Los explosivos hacen +1 de daño y su radio crece una casilla.", mods: new UnitMods { GrenadeDamage = 1, GrenadeRadius = 1 }),
                Stat("thrower", "Brazo largo", "+3 casillas de alcance al lanzar.", mods: new UnitMods { GrenadeRange = 3 }),
                Passive("smokeEyes", "Ojos en el humo", "El humo no protege a los enemigos de sus disparos.", mods: new UnitMods { SmokeSight = 1 }),
                Stat("doubleCharge", "Carga doble", "Los explosivos hacen +1 de daño.", mods: new UnitMods { GrenadeDamage = 1 }),
                Stat("crater", "Cráter", "Los explosivos quitan 1 de blindaje más.", mods: new UnitMods { GrenadeShred = 1 }),
                Stat("fog", "Niebla", "Un humo más, si lleva humo.", spare: Uses(ChargeIds.Smoke, 1)),
                Stat("stoning", "Pedrada", "+2 casillas de alcance al lanzar, y la piedra hace +1 de daño.", mods: new UnitMods { GrenadeRange = 2, StoneDamage = 1 }),
                Stat("blaster", "Artificiero", "Los explosivos hacen +2 de daño.", mods: new UnitMods { GrenadeDamage = 2 }),
                Stat("shockwave", "Onda expansiva", "El radio de los explosivos crece una casilla.", mods: new UnitMods { GrenadeRadius = 1 }),
                Stat("cartridge", "Cartuchera", "Una granada más, si lleva granadas.", spare: Uses(ChargeIds.Grenade, 1)),
                Stat("flares", "Destellos", "Una aturdidora más, si lleva aturdidoras.", spare: Uses(ChargeIds.Flashbang, 1)),
                Stat("lure", "Reclamo", "La piedra se oye 4 casillas más lejos.", mods: new UnitMods { StoneNoise = 4 }),
                Stat("ironArm", "Brazo de hierro", "+3 casillas de alcance al lanzar.", mods: new UnitMods { GrenadeRange = 3 }),
                Stat("sling", "Honda", "La piedra hace +2 de daño.", mods: new UnitMods { StoneDamage = 2 }),
            },
            [SkillIds.Medicine] = new[]
            {
                Stat("bandage", "Vendaje", "El botiquín y las medicinas curan +1.", mods: new UnitMods { Heal = 1 }),
                Passive("calmVoice", "Voz tranquila", "Los compañeros a 4 casillas o menos tienen +10 de voluntad.", mods: new UnitMods { CalmAura = 1 }),
                Active("revival", "Espabilar", "Quita a un compañero de al lado el aturdimiento, la desorientación o el pánico. Sin botiquín.", PerkIds.RevivalProtocol, AbilityIds.Revival),
                Passive("bridge", "Entre los dos", "El botiquín cura 2 más a los soldados del otro jugador.", perk: PerkIds.Bridge),
                Stat("bigKit", "Botiquín grande", "Un uso más del botiquín, si lo lleva.", spare: Uses(ChargeIds.FirstAid, 1)),
                Stat("firstAid", "Arrastrar a cubierto", "Quien recibe su botiquín tiene +10 de defensa hasta su próximo turno.", mods: new UnitMods { Aid = 10 }),
                Passive("lightHands", "Manos de enfermero", "Las medicinas también paran el contagio de un mordido.", perk: PerkIds.LightHands),
                Stat("suture", "Sutura", "El botiquín y las medicinas curan +1.", mods: new UnitMods { Heal = 1 }),
                Stat("bedside", "A la cabecera", "+10 de voluntad.", mods: new UnitMods { Will = 10 }),
                Stat("toughSkin", "Piel curtida", "+1 de salud.", hp: 1),
                Active("restoration", "Pasar visita", "Cura 2 a cada compañero a 4 casillas o menos y les quita los estados alterados. Una vez por misión.", PerkIds.Restoration, AbilityIds.Restoration, charges: Uses(ChargeIds.Restoration, 1)),
                Stat("surgeon", "Cirujano", "El botiquín y las medicinas curan +1.", mods: new UnitMods { Heal = 1 }),
                Passive("lullaby", "Palabras justas", "Los compañeros a 4 casillas o menos tienen 10 más de voluntad.", mods: new UnitMods { CalmAura = 1 }),
                Stat("spares", "Repuestos", "Un uso más del botiquín, si lo lleva.", spare: Uses(ChargeIds.FirstAid, 1)),
                Stat("triage", "Cubrir al herido", "Quien recibe su botiquín tiene 10 más de defensa hasta su próximo turno.", mods: new UnitMods { Aid = 10 }),
                Stat("rounds", "Ronda", "Una vez más por misión, Pasar visita.", charges: Uses(ChargeIds.Restoration, 1)),
                Stat("fieldMedic", "Médico de campaña", "El botiquín y las medicinas curan +2.", mods: new UnitMods { Heal = 2 }),
                Stat("ironBlood", "Hierro en la sangre", "+1 de salud.", hp: 1),
                Passive("cheer", "Ánimo", "Los compañeros a 4 casillas o menos tienen 10 más de voluntad.", mods: new UnitMods { CalmAura = 1 }),
                Stat("courage", "Valor", "+15 de voluntad.", mods: new UnitMods { Will = 15 }),
                Stat("kitBag", "Maletín", "Un uso más del botiquín, si lo lleva.", spare: Uses(ChargeIds.FirstAid, 1)),
                Stat("miracles", "Manos expertas", "El botiquín y las medicinas curan +2.", mods: new UnitMods { Heal = 2 }),
                Passive("presence", "Presencia", "Los compañeros a 4 casillas o menos tienen 10 más de voluntad.", mods: new UnitMods { CalmAura = 1 }),
                Stat("vigor", "Vigor", "+2 de salud.", hp: 2),
                Passive("serenityMind", "Serenidad", "Nada le entra en la cabeza: el grito no le hace nada.", mods: new UnitMods { PsiGuard = 2 }),
                Stat("warKit", "Botiquín de guerra", "Un uso más del botiquín, si lo lleva.", spare: Uses(ChargeIds.FirstAid, 1)),
                Stat("fieldHospital", "Hospital de campaña", "El botiquín y las medicinas curan +1, y quien recibe su botiquín tiene 10 más de defensa hasta su próximo turno.", mods: new UnitMods { Heal = 1, Aid = 10 }),
                Stat("goodHand", "Buena mano", "El botiquín y las medicinas curan +1, y +10 de voluntad.", mods: new UnitMods { Heal = 1, Will = 10 }),
            },
            [SkillIds.Searching] = new[]
            {
                Stat("tinker", "Manitas", "+5 al arreglar.", mods: new UnitMods { Hack = 5 }),
                Stat("lookout", "Buena vista", "+1 casilla de visión.", mods: new UnitMods { Sight = 1 }),
                Passive("quiet", "Sin hacer ruido", "Registrar, abrir cajas y forzar no hacen ruido.", mods: new UnitMods { Quiet = 1 }),
                Active("patchUp", "Apaños", "Arregla 40 de estado del arma propia o de un compañero de al lado. Una vez por misión.", PerkIds.PatchUp, AbilityIds.PatchUp, charges: Uses(ChargeIds.PatchUp, 1)),
                Stat("deft", "Dedos hábiles", "+10 al arreglar.", mods: new UnitMods { Hack = 10 }),
                Stat("crossfire", "Fuego cruzado", "+10 de puntería contra los enemigos a 4 casillas o menos de un soldado del otro jugador.", mods: new UnitMods { SpotterAim = 10 }),
                Passive("care", "Mantenimiento", "Sus armas se gastan la mitad.", mods: new UnitMods { Care = 1 }),
                Stat("sideBySide", "Codo con codo", "+10 de puntería con un soldado del otro jugador a 4 casillas o menos.", mods: new UnitMods { PairAim = 10 }),
                Stat("parapet", "Parapeto", "+10 de defensa con un soldado del otro jugador a 4 casillas o menos.", mods: new UnitMods { PairDefense = 10 }),
                Stat("picklock", "Ganzúa", "+10 al arreglar.", mods: new UnitMods { Hack = 10 }),
                Stat("morePatch", "Más apaños", "Una vez más por misión, Apaños.", charges: Uses(ChargeIds.PatchUp, 1)),
                Stat("binoculars", "Prismáticos", "+1 casilla de visión.", mods: new UnitMods { Sight = 1 }),
                Stat("coolHead", "Cabeza fría", "+10 de voluntad.", mods: new UnitMods { Will = 10 }),
                Stat("shoulder", "Hombro con hombro", "+5 de puntería y +5 de defensa con un soldado del otro jugador a 4 casillas o menos.", mods: new UnitMods { PairAim = 5, PairDefense = 5 }),
                Stat("pointOut", "Señalar", "+10 de puntería contra los enemigos a 4 casillas o menos de un soldado del otro jugador.", mods: new UnitMods { SpotterAim = 10 }),
                Stat("engineer", "Ingeniero", "+15 al arreglar.", mods: new UnitMods { Hack = 15 }),
                Stat("magnifier", "Lupa", "+5 de crítico.", mods: new UnitMods { Crit = 5 }),
                Stat("weakSpot", "Punto débil", "+1 de daño en los críticos.", mods: new UnitMods { CritDamage = 1 }),
                Passive("temperance", "Templanza", "El grito no le hace entrar en pánico.", mods: new UnitMods { PsiGuard = 1 }),
                Stat("lynx", "Vista de lince", "+1 casilla de visión.", mods: new UnitMods { Sight = 1 }),
                Stat("bodge", "Chapuzas", "Una vez más por misión, Apaños.", charges: Uses(ChargeIds.PatchUp, 1)),
                Stat("mastermind", "Mecánico", "+20 al arreglar.", mods: new UnitMods { Hack = 20 }),
                Stat("inseparable", "Uña y carne", "+10 de puntería y +10 de defensa con un soldado del otro jugador a 4 casillas o menos.", mods: new UnitMods { PairAim = 10, PairDefense = 10 }),
                Stat("watchman", "Te cubro", "+15 de puntería contra los enemigos a 4 casillas o menos de un soldado del otro jugador.", mods: new UnitMods { SpotterAim = 15 }),
                Stat("anatomy", "Anatomía", "+10 de crítico.", mods: new UnitMods { Crit = 10 }),
                Stat("watchtower", "Mirador", "+2 casillas de visión.", mods: new UnitMods { Sight = 2 }),
                Stat("shadow", "Sombra", "+5 de defensa y +5 de esquiva.", mods: new UnitMods { Defense = 5, Dodge = 5 }),
                Stat("ingenuity", "Inventiva", "Dos veces más por misión, Apaños.", charges: Uses(ChargeIds.PatchUp, 2)),
            },
            [SkillIds.Fitness] = new[]
            {
                Stat("stamina", "Fondo", "+1 de salud.", hp: 1),
                Stat("crouch", "Agacharse", "+5 de defensa.", mods: new UnitMods { Defense = 5 }),
                Stat("quickReflex", "Reflejos", "+5 de esquiva.", mods: new UnitMods { Dodge = 5 }),
                Stat("sprinter", "Velocista", "+1 casilla de movimiento por acción.", mods: new UnitMods { Mobility = 1 }),
                Stat("lungs", "Pulmones", "+1 de salud.", hp: 1),
                Stat("dodgeFire", "Esquinazo", "Los disparos de reacción contra él, −10 de puntería.", mods: new UnitMods { ReactionDefense = 10 }),
                Active("runAndGun", "Correr y disparar", "Sin gastar acción: una acción más este turno. Enfriamiento 4.", PerkIds.RunAndGun, AbilityIds.RunAndGun),
                Stat("sturdy", "Robusto", "+1 de salud.", hp: 1),
                Stat("temper", "Temple", "+10 de voluntad.", mods: new UnitMods { Will = 10 }),
                Stat("fixedPost", "Puesto fijo", "+10 de defensa en vigilancia.", mods: new UnitMods { WatchDefense = 10 }),
                Passive("shadowstep", "Sin rastro", "Nadie le dispara en vigilancia mientras se mueve.", perk: PerkIds.Shadowstep),
                Stat("leathery", "Correoso", "+1 de salud.", hp: 1),
                Stat("eel", "Anguila", "+10 de esquiva.", mods: new UnitMods { Dodge = 10 }),
                Stat("vest", "Duro de pelar", "+1 de blindaje.", armor: 1),
                Stat("dugIn", "Atrincherado", "+10 de defensa.", mods: new UnitMods { Defense = 10 }),
                Stat("marathon", "Maratón", "+1 casilla de movimiento por acción.", mods: new UnitMods { Mobility = 1 }),
                Stat("ironBody", "De hierro", "+2 de salud.", hp: 2),
                Passive("poise", "Aplomo", "El grito no le hace entrar en pánico.", mods: new UnitMods { PsiGuard = 1 }),
                Stat("feline", "Felino", "Los disparos de reacción contra él, −15 de puntería.", mods: new UnitMods { ReactionDefense = 15 }),
                Stat("ironWill", "Voluntad de hierro", "+15 de voluntad.", mods: new UnitMods { Will = 15 }),
                Stat("agile", "Ágil", "+10 de esquiva.", mods: new UnitMods { Dodge = 10 }),
                Stat("titan", "Corpulento", "+3 de salud.", hp: 3),
                Stat("longRunner", "Fondista", "+1 de salud y +5 de esquiva.", hp: 1, mods: new UnitMods { Dodge = 5 }),
                Stat("rock", "Roca", "+1 de blindaje.", armor: 1),
                Stat("untouchable", "Esquivo", "+15 de esquiva y +5 de defensa.", mods: new UnitMods { Dodge = 15, Defense = 5 }),
                Stat("bulwark", "Baluarte", "+15 de defensa.", mods: new UnitMods { Defense = 15 }),
                Stat("lastStand", "Hasta el final", "+20 de voluntad y +1 de salud.", hp: 1, mods: new UnitMods { Will = 20 }),
                Stat("tireless", "Incansable", "+1 casilla de movimiento por acción.", mods: new UnitMods { Mobility = 1 }),
            },
        };

        // ---------------------------------------------------------------- geometry

        /// <summary>The six axial directions, in the order a ring is walked.</summary>
        private static readonly IReadOnlyList<(int dq, int dr)> Dirs = new[]
        {
            (1, 0),
            (1, -1),
            (0, -1),
            (-1, 0),
            (-1, 1),
            (0, 1),
        };

        /// <summary>
        /// The 169 cells: the core, then ring by ring. Ring `k` is walked from
        /// (−k, k), `k` steps along each direction; the cells of side `i` are sector
        /// `i`'s.
        /// </summary>
        private static List<BoardCell> Build()
        {
            var cells = new List<BoardCell> { new BoardCell { Id = Core, Q = 0, R = 0, Ring = 0, Sector = -1, Line = null, Kind = CellKinds.Core, Name = "Instinto", Effect = "El punto de partida: todo lo demás se abre desde aquí." } };
            for (var k = 1; k <= BoardRings; k++)
            {
                var q = -k;
                var r = k;
                for (var side = 0; side < 6; side++)
                {
                    var line = SectorLines[side];
                    for (var j = 0; j < k; j++)
                    {
                        var skill = Sectors[line][(k * (k - 1)) / 2 + j];
                        cells.Add(skill.Placed(q, r, k, side, line));
                        q += Dirs[side].dq;
                        r += Dirs[side].dr;
                    }
                }
            }
            return cells;
        }

        public static readonly IReadOnlyList<BoardCell> Cells = Build();
        public static readonly IReadOnlyDictionary<string, BoardCell> BoardCells = Cells.ToDictionary((c) => c.Id);
        private static readonly Dictionary<string, string> At = Cells.ToDictionary((c) => $"{c.Q},{c.R}", (c) => c.Id);

        /// <summary>The cells beside `id` on the board.</summary>
        public static List<string> BoardNeighbors(string id)
        {
            if (!BoardCells.TryGetValue(id, out var c)) return new List<string>();
            var out_ = new List<string>();
            foreach (var (dq, dr) in Dirs)
            {
                if (At.TryGetValue($"{c.Q + dq},{c.R + dr}", out var n) && n != "") out_.Add(n);
            }
            return out_;
        }

        /// <summary>Whether `id` lies beside the core or a cell of `board` (the skills unlocked).</summary>
        public static bool WithinReach(IReadOnlyList<string> board, string id)
        {
            return BoardNeighbors(id).Any((n) => n == Core || board.Contains(n));
        }

        /// <summary>The ability each perk of the board gives (abilities.ts abilitiesOf).</summary>
        public static readonly IReadOnlyDictionary<string, string> PerkAbility = Cells.Where((c) => c.Perk != null && c.Ability != null).ToDictionary((c) => c.Perk!, (c) => c.Ability!);

        /// <summary>
        /// A soldier's role (Arturo, 16/10): the line with most cells bought on its
        /// board, shown as a dot of its colour; a tie goes to the line bought last.
        /// Null with nothing bought.
        /// </summary>
        public static string? BoardRole(IReadOnlyList<string> board)
        {
            var count = new Dictionary<string, int>();
            string? best = null;
            foreach (var id in board)
            {
                var line = BoardCells.TryGetValue(id, out var c) ? c.Line : null;
                if (line == null) continue;
                count[line] = (count.TryGetValue(line, out var n) ? n : 0) + 1;
                if (best == null || count[line] >= count[best]) best = line;
            }
            return best;
        }
    }
}
