#nullable enable
// content.ts: gameplay data. Combat numbers mirror XCOM 2's defaults (XComGameCore.ini /
// XComGameData_*.ini) converted to tiles (1 tile = 1.5 m), so the game "feels"
// like the original before we start tuning our own values.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>content.ts RangeTable: `keyof typeof RANGE_TABLES`.</summary>
    public static class RangeTableIds
    {
        public const string Short = "short";
        public const string Medium = "medium";
        public const string Long = "long";
        public const string None = "none";

        public static readonly IReadOnlyList<string> All = new[] { Short, Medium, Long, None };
    }

    /// <summary>content.ts WeaponId.</summary>
    public static class WeaponIds
    {
        public const string Rifle = "rifle";
        public const string Shotgun = "shotgun";
        public const string Cannon = "cannon";
        public const string Sniper = "sniper";
        public const string Pistol = "pistol";
        public const string Sword = "sword";
        public const string Knife = "knife";
        public const string TrooperRifle = "trooperRifle";
        public const string TurretGun = "turretGun";
        public const string OfficerRifle = "officerRifle";
        public const string XenoPistol = "xenoPistol";
        public const string LancerRifle = "lancerRifle";
        public const string StunLance = "stunLance";
        public const string MecCannon = "mecCannon";
        public const string SectoidPistol = "sectoidPistol";
        public const string Claws = "claws";
        public const string FragGrenade = "fragGrenade";
        public const string MicroMissiles = "microMissiles";
        public const string BloaterBurst = "bloaterBurst";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Rifle, Shotgun, Cannon, Sniper, Pistol, Sword, Knife, TrooperRifle, TurretGun, OfficerRifle, XenoPistol,
            LancerRifle, StunLance, MecCannon, SectoidPistol, Claws, FragGrenade, MicroMissiles, BloaterBurst,
        };
    }

    public sealed class WeaponDef
    {
        public string Id { get; set; } = ""; // WeaponIds
        public string Name { get; set; } = "";
        public int Damage { get; set; }
        public int Spread { get; set; }
        /// <summary>Percent chance of +1 damage.</summary>
        public int PlusOne { get; set; }
        public int CritDamage { get; set; }
        /// <summary>0 means the weapon never needs ammo (pistols, blades, explosives).</summary>
        public int Clip { get; set; }
        public int Aim { get; set; }
        public int Crit { get; set; }
        public string Range { get; set; } = ""; // RangeTableIds
        /// <summary>Armour removed per hit.</summary>
        public int Shred { get; set; }
        /// <summary>Action points needed to fire (sniper rifles need both).</summary>
        public int ApCost { get; set; }
        public bool Melee { get; set; }
        /// <summary>Rounds shown per shot — purely visual.</summary>
        public int Burst { get; set; }
        /// <summary>Noise radius in tiles of an attack or a blast with it (docs/FASE2.md).</summary>
        public int Noise { get; set; }
    }

    // ---------------------------------------------------------------- abilities

    /// <summary>content.ts AbilityTarget.</summary>
    public static class AbilityTargets
    {
        public const string Enemy = "enemy";
        public const string Melee = "melee";
        public const string Ally = "ally";
        public const string Tile = "tile";
        public const string Self = "self";
        public const string Corpse = "corpse";

        public static readonly IReadOnlyList<string> All = new[] { Enemy, Melee, Ally, Tile, Self, Corpse };
    }

    /// <summary>AbilityDef.weapon: `'primary' | 'secondary' | 'melee'`.</summary>
    public static class AbilityWeapons
    {
        public const string Primary = "primary";
        public const string Secondary = "secondary";
        public const string Melee = "melee";

        public static readonly IReadOnlyList<string> All = new[] { Primary, Secondary, Melee };
    }

    public sealed class AbilityDef
    {
        public string Id { get; set; } = ""; // AbilityIds
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Target { get; set; } = ""; // AbilityTargets
        /// <summary>Action points it needs; 0 = free action (the unit still needs actions left).</summary>
        public int Ap { get; set; }
        public bool EndsTurn { get; set; }
        public string? Charge { get; set; } // ChargeIds
        /// <summary>Turns before it can be used again.</summary>
        public int? Cooldown { get; set; }
        /// <summary>Tiles, for tile, ally, corpse and psionic abilities.</summary>
        public double? Range { get; set; }
        /// <summary>Blast radius in tiles.</summary>
        public double? Radius { get; set; }
        /// <summary>Weapon whose damage an area ability uses.</summary>
        public string? Explosive { get; set; } // WeaponIds
        /// <summary>Attack variants: which weapon fires and how the shot changes.</summary>
        public string? Weapon { get; set; } // AbilityWeapons
        public int? Aim { get; set; }
        public double? DamageMult { get; set; }
        public int? DamageBonus { get; set; }
        /// <summary>Rounds it needs in the clip.</summary>
        public int? Ammo { get; set; }
        /// <summary>Psionic: the victim's will resists it.</summary>
        public bool? Psi { get; set; }
        /// <summary>
        /// Noise radius in tiles when it isn't its weapon's (docs/FASE2.md; see
        /// abilities.ts noiseOf). Heard where it lands for tile abilities, where the
        /// user stands for the rest.
        /// </summary>
        public int? Noise { get; set; }
    }

    // ------------------------------------------------------------------ items

    /// <summary>FoundWeaponDef.slot: `'primary' | 'melee'`.</summary>
    public static class FoundWeaponSlots
    {
        public const string Primary = "primary";
        public const string Melee = "melee";

        public static readonly IReadOnlyList<string> All = new[] { Primary, Melee };
    }

    public sealed class FoundWeaponDef
    {
        public string Id { get; set; } = ""; // FoundWeaponIds
        /// <summary>As the soldier's card reads it («Fusil de caza · nivel 3»).</summary>
        public string Name { get; set; } = "";
        /// <summary>As it reads when found, with its article.</summary>
        public string Find { get; set; } = "";
        /// <summary>The weapon it fires as.</summary>
        public string Weapon { get; set; } = ""; // WeaponIds
        /// <summary>In the hand (fusil, escopeta, fusil de precisión, ametralladora) or at the belt (the machete).</summary>
        public string Slot { get; set; } = ""; // FoundWeaponSlots
        /// <summary>A good one: it comes with an improvement mounted (`mod`, docs/FASE9.md §5).</summary>
        public bool Better { get; set; }
        public string? Mod { get; set; } // WeaponModIds
        /// <summary>How often it turns up in a weapons box or a search, against the others (0: never).</summary>
        public int Weight { get; set; }
    }

    /// <summary>An element of WEAPON_MODS: `{ id: WeaponModId; name: string; line: string; none: string }`.</summary>
    public sealed class WeaponModDef
    {
        public string Id { get; set; } = ""; // WeaponModIds
        public string Name { get; set; } = "";
        public string Line { get; set; } = "";
        public string None { get; set; } = "";
    }

    public sealed class ItemDef
    {
        public string Id { get; set; } = ""; // ItemIds
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Slot { get; set; } = ""; // ItemSlots
        /// <summary>Price in credits.</summary>
        public int Cost { get; set; }
        /// <summary>Bastion level that unlocks it in the armoury.</summary>
        public int Level { get; set; }
        /// <summary>Uses it adds in a mission.</summary>
        public IReadOnlyDictionary<string, int>? Charges { get; set; } // ChargeIds → uses (Partial<Record>)
        /// <summary>Ability the item grants.</summary>
        public string? Ability { get; set; } // AbilityIds
        /// <summary>`Partial&lt;UnitMods&gt;`: the fields it doesn't name are 0, which adds the same (Content.AddMods).</summary>
        public UnitMods? Mods { get; set; }
        public int? Hp { get; set; }
    }

    // ---------------------------------------------------------------- templates

    public sealed class UnitTemplate
    {
        public string Id { get; set; } = ""; // TemplateIds
        public string Name { get; set; } = "";
        /// <summary>Short label for compact UI.</summary>
        public string Short { get; set; } = "";
        public string Team { get; set; } = ""; // Teams
        public int Hp { get; set; }
        public int Armor { get; set; }
        public int Aim { get; set; }
        public int Defense { get; set; }
        public int Dodge { get; set; }
        /// <summary>Tiles per action point (XCOM 2 mobility 12 m / 1.5).</summary>
        public double Mobility { get; set; } // SOLDIER_MOBILITY * ZOMBIE_PACE for the slow dead: 6.4.
        /// <summary>Sight radius in tiles (27 m / 1.5).</summary>
        public int Sight { get; set; }
        /// <summary>While XCOM is concealed, this unit only notices soldiers this close (12 m / 1.5).</summary>
        public int Detection { get; set; }
        public int Crit { get; set; }
        public int FlankCrit { get; set; }
        public string Weapon { get; set; } = ""; // WeaponIds
        /// <summary>Pistol for snipers.</summary>
        public string? Secondary { get; set; } // WeaponIds
        /// <summary>Blade, lance or claws.</summary>
        public string? MeleeWeapon { get; set; } // WeaponIds
        public IReadOnlyList<string> Abilities { get; set; } = new string[0]; // AbilityIds
        public IReadOnlyDictionary<string, int> Charges { get; set; } = new Dictionary<string, int>(); // ChargeIds → uses (Partial<Record>)
        public bool Squadsight { get; set; }
        public int Will { get; set; }
        /// <summary>Machines: immune to psionics, can't be raised.</summary>
        public bool Robotic { get; set; }
        /// <summary>Zombies (docs/FASE2.md): melee only, never take cover, the horde's AI (ai.ts).</summary>
        public bool Undead { get; set; }
    }

    /// <summary>An element of SOLDIER_NAMES: `{ name: string; sex: 'f' | 'm' }`.</summary>
    public sealed class SoldierName
    {
        public SoldierName(string name, string sex)
        {
            Name = name;
            Sex = sex;
        }

        public string Name { get; }
        public string Sex { get; } // Sexes
    }

    public static class Content
    {
        public const int ApPerTurn = 2;

        /// <summary>Unit.rank from which a soldier has a third action: Veterano (skills.ts VETERANCY, docs/FASE10.md §5).</summary>
        public const int ThirdActionRank = 3;

        /// <summary>Actions a unit starts its turn with: two, three for a soldier from Veterano on.</summary>
        public static int ApPerTurnOf(Unit u)
        {
            return u.Team == Teams.Xcom && u.Rank >= ThirdActionRank ? ApPerTurn + 1 : ApPerTurn;
        }

        /// <summary>Spare magazines a soldier takes into a mission for the primary weapon (docs/FASE7.md): reloading spends one.</summary>
        public const int SpareMags = 3;

        /// <summary>Damage per storey to whoever falls through a floor a grenade broke (docs/FASE7.md §5); armour doesn't stop it.</summary>
        public const int FallDamage = 1;

        public const int LowCoverBonus = 20;
        public const int HighCoverBonus = 40;
        public const int HunkerDefense = 30;
        public const int HunkerDodge = 50;
        /// <summary>Reaction shots lose this fraction of their hit chance (XCOM 2 REACTION_FINALMOD).</summary>
        public const double ReactionPenalty = 0.3;
        public const double GrazeDamageMult = 0.5;
        /// <summary>Squadsight: aim lost per tile beyond the shooter's own sight, and crit penalty.</summary>
        public const int SquadsightDistanceMod = -2;
        public const int SquadsightCritMod = -10;
        public const int MedkitHeal = 4;
        /// <summary>The stone's damage, before Canto afilado and the rest (docs/FASE10.md §4).</summary>
        public const int StoneDamage = 1;

        /// <summary>What is thrown by hand: Lanzar fills with it and its reach reaches it all (docs/FASE10.md §2).</summary>
        public static readonly IReadOnlyCollection<string> Thrown = new HashSet<string>
        {
            AbilityIds.Grenade, AbilityIds.Launch, AbilityIds.Smoke, AbilityIds.Flashbang, AbilityIds.Stone,
        };

        /// <summary>Shooting down from a higher level (XCOM 2 height advantage).</summary>
        public const int HeightAdvantageAim = 20;
        /// <summary>
        /// Cover applies when the attacker is within this angle of the cover's facing.
        /// Wider angles mean fewer flanks. Tuning value, not taken from XCOM.
        /// </summary>
        public const int CoverMaxAngleDeg = 70;

        /// <summary>
        /// Noise (docs/FASE2.md): how far, in tiles, an action is heard. Every enemy
        /// within it that sees no soldier goes to look (`Unit.heard`); noise never
        /// wakes a pod nor breaks concealment by itself, being seen does. Weapons and
        /// abilities carry their own (`noise`); these are the actions that aren't one.
        /// </summary>
        public static class Noise
        {
            /// <summary>Opening a closed door: quiet.</summary>
            public const int Door = 1;
            /// <summary>Going through a window's glass.</summary>
            public const int Breach = 6;
            /// <summary>A move that spends both actions.</summary>
            public const int Dash = 3;
            /// <summary>A failed hack: the alarm blares at the terminal and draws a horde in by the edges.</summary>
            public const int Alarm = 16;
        }

        /// <summary>
        /// The base's traps (docs/FASE12.md §11.4): what a mine does to whoever is
        /// within its radius, the spikes to whoever falls in, and what crossing barbed
        /// wire adds to a step (path.ts costs, in half-tiles).
        /// </summary>
        public static class Traps
        {
            public const int Mine = 4;
            public const double MineRadius = 1.5;
            public const int Spikes = 2;
            public const int Wire = 3;
        }

        /// <summary>
        /// The siege of the base (docs/FASE12.md §4). What a door and a barricade
        /// hold, in damage of the dead's blows (a Zombi takes off 3, a Corredor 2, an
        /// Acorazado 4); how far a blow is heard; the spare magazines each soldier has
        /// at home; and the turns to hold.
        /// </summary>
        public static class Siege
        {
            public const int Door = 18;
            public const int Barricade = 9;
            public const int SmashNoise = 8;
            public const int Mags = 8;
            public const int Hold = 11;
        }

        /// <summary>
        /// The Saqueo's horde (docs/FASE2.md): each squad turn winds its clock up by
        /// `perTurn` points and every noise the squad makes by its radius / `perNoise`
        /// (hordePoints); every `wave` points a wave comes in by the edges. One a turn
        /// at most, and none while `cap` enemies are already on the field. A quiet
        /// squad sees one every 5 turns; a firefight (four rifle shots a turn) one
        /// every 3. Tuned with a headless Saqueo against a scripted squad.
        /// </summary>
        /// <remarks>`trickle`: the dead that come in every turn whatever happens (playtest 2026-10-15, #11), by difficulty (1–4); twice that while a sabotage's alarm sounds.</remarks>
        public static class Horde
        {
            public const int PerTurn = 10;
            public const int PerNoise = 5;
            public const int Wave = 50;
            public const int Cap = 60;
            public static readonly IReadOnlyList<int> Trickle = new[] { 0, 1, 2, 2, 3 };
        }

        /// <summary>
        /// Night missions (docs/ALTURA-Y-NOCHE.md). The living see `sightCut` tiles
        /// less (never under `minSight`) and the dead as ever, but whoever stands in
        /// the light is seen from as far as by day; the enemy detects the hidden squad
        /// at `detection` of its range, unless lit. Lights: the map's street lights
        /// and the base's floodlights. One more group of the dead, the Saqueo's horde
        /// `hordeHead` turns sooner, and `supplies` times the suministros found and
        /// paid. A light reaches only what it sees from its height (storeys above its
        /// floor): a street lamp from `lampHeight`.
        /// </summary>
        public static class Night
        {
            public const int SightCut = 8;
            public const int MinSight = 6;
            public const double Detection = 0.5;
            public const int StreetLight = 2;
            /// <summary>A floodlight of the base (docs/FASE12.md §11.4; Arturo, 18/10: for the sieges by night) lights twice as far.</summary>
            public const int Floodlight = 4;
            public const int LampHeight = 2;
            public const int HordeHead = 1;
            public const double Supplies = 1.5;
        }

        /// <summary>
        /// Infection (docs/FASE3.md): out of 100, the chance that a zombie's melee hit
        /// that draws blood infects, by who bites, and a Hinchado's burst to each it
        /// hurts; a Chaleco antibalas takes `vest` off.
        /// </summary>
        public static class Infection
        {
            public static readonly IReadOnlyDictionary<string, int> Bite = new Dictionary<string, int>
            {
                [TemplateIds.Zombie] = 25,
                [TemplateIds.Xenoid] = 20,
                [TemplateIds.Mec] = 35,
            };
            public const int Burst = 50;
            public const int Vest = 10;
        }

        /// <summary>What a noise of that radius made by the squad adds to the horde's clock.</summary>
        public static int HordePoints(double radius)
        {
            return (int)Math.Floor(radius / Horde.PerNoise);
        }

        /// <summary>XCOM turns available to recover the objective on a recovery mission.</summary>
        public const int RecoveryTurns = 12;

        /// <summary>Aim modifier by tile distance (index = floor(distance)); XCOM 2 conventional tables.</summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<int>> RangeTables = new Dictionary<string, IReadOnlyList<int>>
        {
            [RangeTableIds.Short] = new[] { 0, 40, 35, 32, 28, 23, 19, 16, 12, 6, 2, 0, -2, -4, -7, -10, -12, -15, -17, -18, -18, -19, -19, -21, -25, -30 },
            [RangeTableIds.Medium] = new[] { 0, 20, 19, 17, 16, 13, 11, 9, 7, 5, 4, 3, 2, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            [RangeTableIds.Long] = new[] { 0, -30, -27, -24, -21, -18, -15, -12, -9, -6, -3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            [RangeTableIds.None] = new[] { 0 },
        };

        public static int RangeModifier(string table, double distance)
        {
            var values = RangeTables[table];
            return values[(int)Math.Min(Math.Floor(distance), values.Count - 1)];
        }

        private static WeaponDef Weapon(
            string id,
            string name,
            int damage,
            int spread = 0,
            int plusOne = 0,
            int critDamage = 0,
            int clip = 0,
            int aim = 0,
            int crit = 0,
            string range = RangeTableIds.Medium,
            int shred = 0,
            int apCost = 1,
            bool melee = false,
            int burst = 3,
            int noise = 0)
        {
            return new WeaponDef
            {
                Id = id,
                Name = name,
                Damage = damage,
                Spread = spread,
                PlusOne = plusOne,
                CritDamage = critDamage,
                Clip = clip,
                Aim = aim,
                Crit = crit,
                Range = range,
                Shred = shred,
                ApCost = apCost,
                Melee = melee,
                Burst = burst,
                Noise = noise,
            };
        }

        public static readonly IReadOnlyDictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>
        {
            // The squad's guns are heard further (Arturo, 17/10: «disparar debería hacer más ruido»), and a grenade further still.
            [WeaponIds.Rifle] = Weapon(WeaponIds.Rifle, "Fusil de asalto", 4, spread: 1, critDamage: 2, clip: 4, noise: 18),
            [WeaponIds.Shotgun] = Weapon(WeaponIds.Shotgun, "Escopeta", 5, spread: 1, critDamage: 3, clip: 4, crit: 10, range: RangeTableIds.Short, burst: 1, noise: 16),
            [WeaponIds.Cannon] = Weapon(WeaponIds.Cannon, "Ametralladora", 5, spread: 1, critDamage: 2, clip: 3, burst: 5, noise: 20),
            [WeaponIds.Sniper] = Weapon(WeaponIds.Sniper, "Fusil de precisión", 5, spread: 1, critDamage: 2, clip: 3, crit: 10, range: RangeTableIds.Long, apCost: 2, burst: 1, noise: 22),
            [WeaponIds.Pistol] = Weapon(WeaponIds.Pistol, "Pistola", 2, plusOne: 50, critDamage: 1, burst: 1, noise: 12),
            [WeaponIds.Sword] = Weapon(WeaponIds.Sword, "Machete", 4, spread: 1, critDamage: 2, aim: 20, crit: 10, range: RangeTableIds.None, melee: true, burst: 1, noise: 1),
            // Everyone's blade in the campaign (docs/FASE8.md §A), until a machete.
            [WeaponIds.Knife] = Weapon(WeaponIds.Knife, "Cuchillo", 3, spread: 1, critDamage: 1, aim: 10, range: RangeTableIds.None, melee: true, burst: 1, noise: 1),
            [WeaponIds.TrooperRifle] = Weapon(WeaponIds.TrooperRifle, "Fusil de caza", 3, critDamage: 1, clip: 3, noise: 12),
            // The base's turret (docs/FASE12.md §11.4): its clip is its piece's charges (base.ts PIECES.turret), reloaded at home with piezas.
            [WeaponIds.TurretGun] = Weapon(WeaponIds.TurretGun, "Torreta", 3, critDamage: 1, clip: 6, aim: 5, noise: 16),
            [WeaponIds.OfficerRifle] = Weapon(WeaponIds.OfficerRifle, "Fusil automático", 3, plusOne: 50, critDamage: 2, clip: 3, noise: 14),
            [WeaponIds.LancerRifle] = Weapon(WeaponIds.LancerRifle, "Carabina", 3, plusOne: 50, critDamage: 2, clip: 3, noise: 12),
            [WeaponIds.StunLance] = Weapon(WeaponIds.StunLance, "Red y porra", 3, spread: 1, critDamage: 2, range: RangeTableIds.None, melee: true, burst: 1, noise: 1),
            // The zombies' bodies (docs/FASE2.md): no guns. The ids are the XCOM 2 ones they replaced.
            [WeaponIds.Claws] = Weapon(WeaponIds.Claws, "Mordisco", 3, spread: 1, critDamage: 2, aim: 10, range: RangeTableIds.None, melee: true, burst: 1),
            [WeaponIds.XenoPistol] = Weapon(WeaponIds.XenoPistol, "Zarpazo", 2, spread: 1, critDamage: 2, aim: 10, range: RangeTableIds.None, melee: true, burst: 1),
            [WeaponIds.MecCannon] = Weapon(WeaponIds.MecCannon, "Embestida", 4, spread: 1, critDamage: 2, shred: 1, range: RangeTableIds.None, melee: true, burst: 1),
            // The Chillón doesn't bite: it only screams (mindspin).
            [WeaponIds.SectoidPistol] = Weapon(WeaponIds.SectoidPistol, "Chillido", 0, range: RangeTableIds.None, burst: 0),
            [WeaponIds.BloaterBurst] = Weapon(WeaponIds.BloaterBurst, "Reventón", 3, burst: 0, noise: 6),
            [WeaponIds.FragGrenade] = Weapon(WeaponIds.FragGrenade, "Granada de fragmentación", 3, plusOne: 20, shred: 1, burst: 0, noise: 26),
            [WeaponIds.MicroMissiles] = Weapon(WeaponIds.MicroMissiles, "Micromisiles", 3, shred: 1, burst: 0, noise: 18),
        };

        // ---------------------------------------------------------------- abilities

        public static readonly IReadOnlyDictionary<string, AbilityDef> Abilities = new Dictionary<string, AbilityDef>
        {
            [AbilityIds.Shoot] = new AbilityDef { Id = AbilityIds.Shoot, Name = "Disparar", Description = "Dispara el arma principal.", Target = AbilityTargets.Enemy, Ap = 1, EndsTurn = false, Weapon = AbilityWeapons.Primary },
            [AbilityIds.Pistol] = new AbilityDef { Id = AbilityIds.Pistol, Name = "Pistola", Description = "Dispara la pistola, sin gastar las balas del arma principal.", Target = AbilityTargets.Enemy, Ap = 1, EndsTurn = false, Weapon = AbilityWeapons.Secondary },
            // An action, like a shot (playtest 16/10): the turn goes on.
            [AbilityIds.Slash] = new AbilityDef { Id = AbilityIds.Slash, Name = "Tajo", Description = "Golpea con la hoja a un enemigo que tengas al lado. Ignora la cobertura.", Target = AbilityTargets.Melee, Ap = 1, EndsTurn = false, Weapon = AbilityWeapons.Melee },
            [AbilityIds.StunLance] = new AbilityDef { Id = AbilityIds.StunLance, Name = "Red y porra", Description = "Atrapa con la red y golpea con la porra: aturde.", Target = AbilityTargets.Melee, Ap = 1, EndsTurn = true, Weapon = AbilityWeapons.Melee },
            // An action, and the turn goes on: with two, a bite and a step, or two bites (playtest 16/10 #21).
            [AbilityIds.Claw] = new AbilityDef { Id = AbilityIds.Claw, Name = "Mordisco", Description = "Muerde a quien tiene al lado.", Target = AbilityTargets.Melee, Ap = 1, EndsTurn = false, Weapon = AbilityWeapons.Melee },
            [AbilityIds.Overwatch] = new AbilityDef { Id = AbilityIds.Overwatch, Name = "Vigilancia", Description = "Dispara al primer enemigo que se mueva a la vista.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = true },
            // Fase 10 (docs/FASE10.md §4): the reach is the throw's (abilityRange), the noise grows with Señuelo and Reclamo.
            [AbilityIds.Stone] = new AbilityDef { Id = AbilityIds.Stone, Name = "Piedra", Description = "Lanza una piedra: suena donde cae y los zombis van a mirar. Si le da a alguien, 1 de daño.", Target = AbilityTargets.Tile, Ap = 1, EndsTurn = false, Range = 10, Noise = 8 },
            [AbilityIds.OverwatchMelee] = new AbilityDef { Id = AbilityIds.OverwatchMelee, Name = "Vigilancia cuerpo a cuerpo", Description = "Golpea con la hoja al primer enemigo que se le ponga al lado.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = true },
            [AbilityIds.Hunker] = new AbilityDef { Id = AbilityIds.Hunker, Name = "Cubrirse", Description = "+30 de defensa y +50 de esquiva hasta tu próximo turno.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = true },
            [AbilityIds.Reload] = new AbilityDef { Id = AbilityIds.Reload, Name = "Recargar", Description = "Mete un cargador lleno y gasta uno de repuesto: primero en el arma principal y, si está llena, en la pistola. Los de repuesto los hace el Taller; registrando aparecen más.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = false },
            [AbilityIds.Grenade] = new AbilityDef { Id = AbilityIds.Grenade, Name = "Granada", Description = "Lanza una granada: daña a todos en el área, destruye cobertura y abre boquetes en las paredes. Arriba hunde el suelo donde cae, y quien esté encima cae.", Target = AbilityTargets.Tile, Ap = 1, EndsTurn = true, Charge = ChargeIds.Grenade, Range = 10, Radius = 2.5, Explosive = WeaponIds.FragGrenade },
            [AbilityIds.Launch] = new AbilityDef { Id = AbilityIds.Launch, Name = "Lanzagranadas", Description = "Dispara una granada más lejos y con más radio. También abre boquetes en las paredes y, arriba, hunde el suelo donde cae.", Target = AbilityTargets.Tile, Ap = 1, EndsTurn = true, Charge = ChargeIds.Grenade, Range = 14, Radius = 3.5, Explosive = WeaponIds.FragGrenade },
            [AbilityIds.Missiles] = new AbilityDef { Id = AbilityIds.Missiles, Name = "Granada del chaleco", Description = "Le estalla una granada que aún cuelga del chaleco. También abre boquetes en las paredes y, arriba, hunde el suelo.", Target = AbilityTargets.Tile, Ap = 1, EndsTurn = true, Charge = ChargeIds.Missiles, Range = 15, Radius = 2.5, Explosive = WeaponIds.MicroMissiles },
            [AbilityIds.Evac] = new AbilityDef { Id = AbilityIds.Evac, Name = "Evacuar", Description = "Desde La Lata, pide salir. Si el otro jugador acepta, salen los que estén dentro y los de fuera se pierden. En la Limpieza es una retirada: la misión se pierde.", Target = AbilityTargets.Self, Ap = 0, EndsTurn = false },
            [AbilityIds.Hack] = new AbilityDef
            {
                Id = AbilityIds.Hack,
                Name = "Arreglar",
                Description = "Arregla el equipo (una radio, un repetidor…) desde una casilla de al lado. Si falla, salta la alarma: se oye lejos y atrae a los muertos por los bordes.",
                Target = AbilityTargets.Self,
                Ap = 1,
                EndsTurn = true,
                Noise = 2,
            },
            // Lore's texts (docs/FASE8.md §B).
            [AbilityIds.Search] = new AbilityDef
            {
                Id = AbilityIds.Search,
                Name = "Abrir la caja",
                Description = "Abre una caja marcada que tengas al lado. Da siempre lo suyo: un arma, una o dos medicinas, dos cargadores o de 3 a 5 raciones. Hace un poco de ruido.",
                Target = AbilityTargets.Tile,
                Ap = 1,
                EndsTurn = false,
                Range = 1.5,
                Noise = 2,
            },
            [AbilityIds.SearchRoom] = new AbilityDef
            {
                Id = AbilityIds.SearchRoom,
                Name = "Registrar la habitación",
                Description = "Registra la habitación en la que estás. Sale lo que haya según el sitio: suministros, comida, piezas, medicinas, algún cargador y, a veces, un arma. Cada habitación se registra una vez, para toda la escuadra. Hace un poco de ruido.",
                Target = AbilityTargets.Self,
                Ap = 1,
                EndsTurn = false,
                Noise = 2,
            },
            // A door beside the soldier, without moving: free, and as quiet as walking through it (NOISE.door). Texts are placeholders for Lore.
            [AbilityIds.OpenDoor] = new AbilityDef { Id = AbilityIds.OpenDoor, Name = "Abrir la puerta", Description = "Abre una puerta cerrada que tengas al lado.", Target = AbilityTargets.Tile, Ap = 0, EndsTurn = false, Range = 1.5, Noise = 1 },
            [AbilityIds.CloseDoor] = new AbilityDef { Id = AbilityIds.CloseDoor, Name = "Cerrar la puerta", Description = "Cierra una puerta abierta que tengas al lado, si no hay nadie en ella.", Target = AbilityTargets.Tile, Ap = 0, EndsTurn = false, Range = 1.5, Noise = 1 },
            [AbilityIds.RunAndGun] = new AbilityDef { Id = AbilityIds.RunAndGun, Name = "Correr y disparar", Description = "Una acción más este turno.", Target = AbilityTargets.Self, Ap = 0, EndsTurn = false, Cooldown = 4 },
            [AbilityIds.RapidFire] = new AbilityDef { Id = AbilityIds.RapidFire, Name = "Ráfaga", Description = "Dos disparos seguidos, cada uno con −15 de puntería. Gasta dos balas.", Target = AbilityTargets.Enemy, Ap = 1, EndsTurn = false, Cooldown = 3, Weapon = AbilityWeapons.Primary, Aim = -15, Ammo = 2 },
            [AbilityIds.Suppression] = new AbilityDef { Id = AbilityIds.Suppression, Name = "Supresión", Description = "Inmoviliza al objetivo: −50 de puntería hasta tu próximo turno, y si se mueve, le disparas. Gasta dos balas.", Target = AbilityTargets.Enemy, Ap = 1, EndsTurn = true, Ammo = 2 },
            [AbilityIds.ChainShot] = new AbilityDef { Id = AbilityIds.ChainShot, Name = "Disparo en cadena", Description = "Disparo con −15 de puntería; si acierta, vuelves a disparar.", Target = AbilityTargets.Enemy, Ap = 1, EndsTurn = false, Cooldown = 3, Weapon = AbilityWeapons.Primary, Aim = -15 },
            [AbilityIds.Rupture] = new AbilityDef { Id = AbilityIds.Rupture, Name = "Ruptura", Description = "Disparo con +3 de daño; el objetivo recibe +3 de daño de todos los ataques.", Target = AbilityTargets.Enemy, Ap = 1, EndsTurn = false, Cooldown = 4, Weapon = AbilityWeapons.Primary, DamageBonus = 3 },
            [AbilityIds.AimedShot] = new AbilityDef { Id = AbilityIds.AimedShot, Name = "A la cabeza", Description = "Un disparo con −25 de puntería que hace la mitad más de daño.", Target = AbilityTargets.Enemy, Ap = 1, EndsTurn = false, Cooldown = 3, Weapon = AbilityWeapons.Primary, Aim = -25, DamageMult = 1.5 },
            [AbilityIds.LightningHands] = new AbilityDef { Id = AbilityIds.LightningHands, Name = "Manos rápidas", Description = "Un disparo rápido con la pistola.", Target = AbilityTargets.Enemy, Ap = 0, EndsTurn = false, Cooldown = 4, Weapon = AbilityWeapons.Secondary },
            [AbilityIds.Faceoff] = new AbilityDef { Id = AbilityIds.Faceoff, Name = "Cara a cara", Description = "Un disparo de pistola a cada enemigo que veas.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = true, Cooldown = 4, Weapon = AbilityWeapons.Secondary },
            [AbilityIds.KillZone] = new AbilityDef { Id = AbilityIds.KillZone, Name = "Nadie pasa", Description = "Vigilancia que dispara a cada enemigo que se mueva a la vista, una vez a cada uno.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = true, Cooldown = 4 },
            // Fase 9 (skills.ts): Espabilar without a medkit, Pasar visita once a mission.
            [AbilityIds.Revival] = new AbilityDef { Id = AbilityIds.Revival, Name = "Espabilar", Description = "Quita a un compañero de al lado el aturdimiento, la desorientación o el pánico. Sin botiquín.", Target = AbilityTargets.Ally, Ap = 1, EndsTurn = false, Range = 1.5 },
            [AbilityIds.Restoration] = new AbilityDef { Id = AbilityIds.Restoration, Name = "Pasar visita", Description = "Cura 2 a cada compañero a 4 casillas o menos y les quita los estados alterados. Una vez por misión.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = true, Charge = ChargeIds.Restoration, Range = 4 },
            [AbilityIds.ElbowRoom] = new AbilityDef { Id = AbilityIds.ElbowRoom, Name = "Hacerse sitio", Description = "Un Tajo a cada enemigo que tengas al lado.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = true, Cooldown = 3, Weapon = AbilityWeapons.Melee },
            [AbilityIds.PatchUp] = new AbilityDef { Id = AbilityIds.PatchUp, Name = "Apaños", Description = "Arregla 40 de estado del arma propia o de un compañero de al lado. Una vez por misión.", Target = AbilityTargets.Ally, Ap = 1, EndsTurn = false, Charge = ChargeIds.PatchUp, Range = 1.5 },
            [AbilityIds.Mindspin] = new AbilityDef { Id = AbilityIds.Mindspin, Name = "Grito", Description = "El Chillón grita: desorienta o asusta a los soldados cercanos, y todo zombi a 16 casillas lo oye y viene.", Target = AbilityTargets.Enemy, Ap = 1, EndsTurn = true, Cooldown = 3, Range = 14, Psi = true, Noise = 16 },
            [AbilityIds.Reanimate] = new AbilityDef { Id = AbilityIds.Reanimate, Name = "Despertar a un muerto", Description = "El grito del Chillón despierta un cadáver, que se levanta como Zombi.", Target = AbilityTargets.Corpse, Ap = 1, EndsTurn = true, Cooldown = 4, Range = 12, Psi = true, Noise = 8 },
            [AbilityIds.Burst] = new AbilityDef { Id = AbilityIds.Burst, Name = "Reventón", Description = "Al morir o al llegar a un soldado revienta: 3 de daño a quien tenga al lado.", Target = AbilityTargets.Self, Ap = 1, EndsTurn = true, Radius = 1.5, Explosive = WeaponIds.BloaterBurst },
            [AbilityIds.Smash] = new AbilityDef { Id = AbilityIds.Smash, Name = "Golpear", Description = "Golpea la puerta o la barricada que tiene al lado hasta tirarla. Se oye, y acuden más.", Target = AbilityTargets.Tile, Ap = 1, EndsTurn = false, Range = 1.5, Noise = Siege.SmashNoise },
            [AbilityIds.Smoke] = new AbilityDef { Id = AbilityIds.Smoke, Name = "Granada de humo", Description = "Cubre una zona de humo durante 2 turnos: −20 a la puntería contra quien esté dentro.", Target = AbilityTargets.Tile, Ap = 1, EndsTurn = true, Charge = ChargeIds.Smoke, Range = 10, Radius = 2, Noise = 4 },
            [AbilityIds.Flashbang] = new AbilityDef { Id = AbilityIds.Flashbang, Name = "Granada aturdidora", Description = "Desorienta a los enemigos de la zona (−20 de puntería, sin explosivos). No hace daño.", Target = AbilityTargets.Tile, Ap = 1, EndsTurn = true, Charge = ChargeIds.Flashbang, Range = 10, Radius = 2.5, Noise = 18 },
            [AbilityIds.FirstAid] = new AbilityDef { Id = AbilityIds.FirstAid, Name = "Botiquín", Description = "Cura 4 de salud a uno mismo o a un compañero de al lado.", Target = AbilityTargets.Ally, Ap = 1, EndsTurn = false, Charge = ChargeIds.FirstAid, Range = 1.5 },
        };

        /// <summary>Base chance of fixing the objective terminal (Arreglar); Investigar's bars add to it.</summary>
        public const int HackBase = 55;

        /// <summary>Mindspin: success chance is PSI_BASE − (will − 40), then the effect is rolled.</summary>
        public const int PsiBase = 70;
        public const int DisorientedAim = 20;
        public const int SuppressedAim = 50;
        public const int HoloAim = 15;
        public const int RuptureDamage = 3;
        /// <summary>Turns a mind-controlled soldier fights for the aliens (ends early if the sectoid dies).</summary>
        public const int ControlTurns = 2;
        public const int RestorationHeal = 2;
        public const int SmokeDefense = 20;
        public const int SmokeTurns = 2;

        // ------------------------------------------------------------------ items

        /// <summary>Things a soldier can carry back from searching (docs/FASE2.md): a full backpack can't search.</summary>
        public const int BackpackSlots = 3;
        /// <summary>What a medicine heals (docs/FASE8.md §C): only health; stopping a bite's contagion is unlocked.</summary>
        public const int MedicineHeal = 3;

        /// <summary>Rounds in a pistol's clip when it counts them (docs/FASE8.md §A: the campaign's soldiers; in a skirmish it never runs out).</summary>
        public const int PistolClip = 6;

        /// <summary>
        /// The Armería's weapons (docs/FASE8.md §A): the campaign's soldiers start with a
        /// knife and a pistol, and these are found, made in the Taller or brought by the
        /// Bastion's level. Anyone may carry any of them. The good ones (docs/FASE4.md)
        /// fight a tier above. Names are placeholders for Lore.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, FoundWeaponDef> FoundWeapons = new Dictionary<string, FoundWeaponDef>
        {
            [FoundWeaponIds.AssaultRifle] = new FoundWeaponDef { Id = FoundWeaponIds.AssaultRifle, Name = "Fusil de asalto", Find = "un fusil de asalto", Weapon = WeaponIds.Rifle, Slot = FoundWeaponSlots.Primary, Better = false, Weight = 8 },
            [FoundWeaponIds.PumpShotgun] = new FoundWeaponDef { Id = FoundWeaponIds.PumpShotgun, Name = "Escopeta", Find = "una escopeta", Weapon = WeaponIds.Shotgun, Slot = FoundWeaponSlots.Primary, Better = false, Weight = 6 },
            [FoundWeaponIds.PrecisionRifle] = new FoundWeaponDef { Id = FoundWeaponIds.PrecisionRifle, Name = "Fusil de precisión", Find = "un fusil de precisión", Weapon = WeaponIds.Sniper, Slot = FoundWeaponSlots.Primary, Better = false, Weight = 3 },
            [FoundWeaponIds.MachineGun] = new FoundWeaponDef { Id = FoundWeaponIds.MachineGun, Name = "Ametralladora", Find = "una ametralladora", Weapon = WeaponIds.Cannon, Slot = FoundWeaponSlots.Primary, Better = false, Weight = 3 },
            [FoundWeaponIds.Machete] = new FoundWeaponDef { Id = FoundWeaponIds.Machete, Name = "Machete", Find = "un machete", Weapon = WeaponIds.Sword, Slot = FoundWeaponSlots.Melee, Better = false, Weight = 0 },
            [FoundWeaponIds.HuntingRifle] = new FoundWeaponDef { Id = FoundWeaponIds.HuntingRifle, Name = "Fusil de cerrojo", Find = "un fusil de cerrojo bien engrasado", Weapon = WeaponIds.Rifle, Slot = FoundWeaponSlots.Primary, Better = true, Mod = WeaponModIds.Stock, Weight = 3 },
            [FoundWeaponIds.SawnOff] = new FoundWeaponDef { Id = FoundWeaponIds.SawnOff, Name = "Escopeta recortada", Find = "una escopeta recortada", Weapon = WeaponIds.Shotgun, Slot = FoundWeaponSlots.Primary, Better = true, Mod = WeaponModIds.ExtMag, Weight = 2 },
            [FoundWeaponIds.ScopedRifle] = new FoundWeaponDef { Id = FoundWeaponIds.ScopedRifle, Name = "Fusil con mira", Find = "un fusil con mira telescópica", Weapon = WeaponIds.Sniper, Slot = FoundWeaponSlots.Primary, Better = true, Mod = WeaponModIds.Scope, Weight = 1 },
            [FoundWeaponIds.LightMachineGun] = new FoundWeaponDef { Id = FoundWeaponIds.LightMachineGun, Name = "Ametralladora ligera", Find = "una ametralladora ligera con su cinta", Weapon = WeaponIds.Cannon, Slot = FoundWeaponSlots.Primary, Better = true, Mod = WeaponModIds.ExtMag, Weight = 1 },
        };

        // ------------------------------------------------------- condition and mods

        /// <summary>A weapon's improvements (docs/FASE9.md §5; names and lines, Lore's).</summary>
        public static readonly IReadOnlyDictionary<string, WeaponModDef> WeaponMods = new Dictionary<string, WeaponModDef>
        {
            [WeaponModIds.Scope] = new WeaponModDef { Id = WeaponModIds.Scope, Name = "Mira", Line = "+10 de puntería.", None = "ninguna mira" },
            [WeaponModIds.Stock] = new WeaponModDef { Id = WeaponModIds.Stock, Name = "Culata", Line = "+10 de puntería en vigilancia: el arma apoyada no tiembla.", None = "ninguna culata" },
            [WeaponModIds.ExtMag] = new WeaponModDef { Id = WeaponModIds.ExtMag, Name = "Cargador ampliado", Line = "Dos balas más en cada cargador.", None = "ningún cargador ampliado" },
            [WeaponModIds.Silencer] = new WeaponModDef { Id = WeaponModIds.Silencer, Name = "Silenciador", Line = "El disparo hace la mitad de ruido. Casero: un tubo, arandelas y cinta.", None = "ningún silenciador" },
        };
        public const int ModSlots = 2;
        public const int ModAim = 10;
        public const int ExtMag = 2;
        /// <summary>Below this a weapon is «Gastada»: −WORN_AIM aim. At 0, «Rota»: it doesn't fire.</summary>
        public const int WornBelow = 30;
        public const int WornAim = 10;
        /// <summary>The Armería mends this much condition for every part.</summary>
        public const int RepairPerPart = 25;
        /// <summary>What Apaños mends in the field (docs/FASE9.md).</summary>
        public const int PatchUp = 40;

        /// <summary>What a shot takes off a weapon's condition: the precision rifle two, the rest one (the machine gun, one per burst).</summary>
        public static int WearOf(string weapon)
        {
            return weapon == WeaponIds.Sniper ? 2 : 1;
        }

        /// <summary>Whether `ability` fires the primary weapon a soldier carries (not the pistol beside it, not a blade).</summary>
        public static bool FiresPrimary(Unit u, string ability)
        {
            return u.Arms?.Primary != null && u.Arms.Primary != "" && (Abilities[ability].Weapon ?? AbilityWeapons.Primary) == AbilityWeapons.Primary && (IsAttack(ability) || ability == AbilityIds.Overwatch || ability == AbilityIds.KillZone || ability == AbilityIds.Suppression);
        }

        /// <summary>The condition of the primary weapon a soldier carries (100 for none, or none told).</summary>
        public static double ConditionOf(Unit u)
        {
            return !string.IsNullOrEmpty(u.Arms?.Primary) ? (u.Arms!.Condition ?? 100) : 100;
        }

        public static bool HasMod(Unit u, string mod)
        {
            return !string.IsNullOrEmpty(u.Arms?.Primary) && u.Arms!.Mods != null && u.Arms.Mods.Contains(mod);
        }

        /// <summary>The Armería's plain weapon of each class weapon: what an older campaign's soldiers keep (docs/FASE8.md §A).</summary>
        public static readonly IReadOnlyDictionary<string, string> PlainWeapon = new Dictionary<string, string>
        {
            [WeaponIds.Rifle] = FoundWeaponIds.AssaultRifle,
            [WeaponIds.Shotgun] = FoundWeaponIds.PumpShotgun,
            [WeaponIds.Sniper] = FoundWeaponIds.PrecisionRifle,
            [WeaponIds.Cannon] = FoundWeaponIds.MachineGun,
            [WeaponIds.Sword] = FoundWeaponIds.Machete,
        };

        public static readonly IReadOnlyDictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>
        {
            // The belt (docs/FASE9.md §4): two of these; ammunition goes into the weapons' mods instead.
            [ItemIds.FragGrenade] = new ItemDef { Id = ItemIds.FragGrenade, Name = "Granadas", Description = "Dos granadas de fragmentación, en el cinturón. Quien no las lleva, no puede lanzar.", Slot = ItemSlots.Utility, Cost = 20, Level = 1, Charges = new Dictionary<string, int> { [ChargeIds.Grenade] = 2 } },
            [ItemIds.SmokeGrenade] = new ItemDef { Id = ItemIds.SmokeGrenade, Name = "Granada de humo", Description = "Humo durante 2 turnos: −20 a la puntería contra quien esté dentro.", Slot = ItemSlots.Utility, Cost = 25, Level = 1, Charges = new Dictionary<string, int> { [ChargeIds.Smoke] = 1 }, Ability = AbilityIds.Smoke },
            [ItemIds.FirstAid] = new ItemDef { Id = ItemIds.FirstAid, Name = "Botiquín", Description = "Dos usos: cura 4 a uno mismo o a un compañero de al lado.", Slot = ItemSlots.Utility, Cost = 25, Level = 1, Charges = new Dictionary<string, int> { [ChargeIds.FirstAid] = 2 }, Ability = AbilityIds.FirstAid },
            [ItemIds.Flashbang] = new ItemDef { Id = ItemIds.Flashbang, Name = "Granada aturdidora", Description = "Desorienta a los enemigos de la zona.", Slot = ItemSlots.Utility, Cost = 30, Level = 2, Charges = new Dictionary<string, int> { [ChargeIds.Flashbang] = 1 }, Ability = AbilityIds.Flashbang },
            [ItemIds.TracerRounds] = new ItemDef { Id = ItemIds.TracerRounds, Name = "Munición trazadora", Description = "+10 de puntería.", Slot = ItemSlots.Ammo, Cost = 35, Level = 2, Mods = new UnitMods { Aim = 10 } },
            [ItemIds.NanoVest] = new ItemDef { Id = ItemIds.NanoVest, Name = "Chaleco antibalas", Description = "+2 de salud.", Slot = ItemSlots.Utility, Cost = 50, Level = 4, Hp = 2 },
            [ItemIds.ApRounds] = new ItemDef { Id = ItemIds.ApRounds, Name = "Munición perforante", Description = "El arma principal ignora 1 punto de blindaje.", Slot = ItemSlots.Ammo, Cost = 55, Level = 4, Mods = new UnitMods { Pierce = 1 } },
            [ItemIds.MindShield] = new ItemDef { Id = ItemIds.MindShield, Name = "Tapones", Description = "Cera en los oídos: el grito del Chillón no te afecta.", Slot = ItemSlots.Utility, Cost = 75, Level = 6, Mods = new UnitMods { Will = 100 } },
        };

        // ---------------------------------------------------------------- templates

        /// <summary>Tiles a soldier covers in an action, and how much of that the dead do (playtest 16/10 #21; 80 %, not 60, Ana's playtest 28/10).</summary>
        public const int SoldierMobility = 8;
        public const double ZombiePace = 0.8;

        private static UnitTemplate Soldier(
            string id,
            string name,
            string @short,
            string weapon,
            string[] abilities,
            IReadOnlyDictionary<string, int> charges,
            int hp = 5,
            int armor = 0,
            int aim = 65,
            int defense = 0,
            int dodge = 0,
            double mobility = SoldierMobility,
            int sight = 18,
            int detection = 0,
            int crit = 0,
            int flankCrit = 50,
            bool squadsight = false,
            int will = 40,
            bool robotic = false,
            bool undead = false,
            string? secondary = null,
            string? meleeWeapon = null)
        {
            return new UnitTemplate
            {
                Id = id,
                Name = name,
                Short = @short,
                Team = Teams.Xcom,
                Hp = hp,
                Armor = armor,
                Aim = aim,
                Defense = defense,
                Dodge = dodge,
                Mobility = mobility,
                Sight = sight,
                Detection = detection,
                Crit = crit,
                FlankCrit = flankCrit,
                Weapon = weapon,
                Secondary = secondary,
                MeleeWeapon = meleeWeapon,
                Abilities = abilities,
                Charges = charges,
                Squadsight = squadsight,
                Will = will,
                Robotic = robotic,
                Undead = undead,
            };
        }

        private static UnitTemplate Alien(
            string id,
            string name,
            string @short,
            string weapon,
            string[] abilities,
            int hp = 3,
            int armor = 0,
            int aim = 65,
            int defense = 0,
            int dodge = 0,
            double mobility = 8,
            int sight = 18,
            int detection = 8,
            int crit = 0,
            int flankCrit = 33,
            IReadOnlyDictionary<string, int>? charges = null,
            bool squadsight = false,
            int will = 60,
            bool robotic = false,
            bool undead = false,
            string? secondary = null,
            string? meleeWeapon = null)
        {
            return new UnitTemplate
            {
                Id = id,
                Name = name,
                Short = @short,
                Team = Teams.Alien,
                Hp = hp,
                Armor = armor,
                Aim = aim,
                Defense = defense,
                Dodge = dodge,
                Mobility = mobility,
                Sight = sight,
                Detection = detection,
                Crit = crit,
                FlankCrit = flankCrit,
                Weapon = weapon,
                Secondary = secondary,
                MeleeWeapon = meleeWeapon,
                Abilities = abilities,
                Charges = charges ?? new Dictionary<string, int>(),
                Squadsight = squadsight,
                Will = will,
                Robotic = robotic,
                Undead = undead,
            };
        }

        private static IReadOnlyDictionary<string, int> Charges(string? id = null, int uses = 0)
        {
            var out_ = new Dictionary<string, int>();
            if (id != null) out_[id] = uses;
            return out_;
        }

        public static readonly IReadOnlyDictionary<string, UnitTemplate> Templates = new Dictionary<string, UnitTemplate>
        {
            [TemplateIds.Assault] = Soldier(
                TemplateIds.Assault, "Asalto", "ASL", WeaponIds.Shotgun, meleeWeapon: WeaponIds.Sword,
                abilities: new[] { AbilityIds.Shoot, AbilityIds.Slash, AbilityIds.Overwatch, AbilityIds.Grenade, AbilityIds.Reload, AbilityIds.Hunker }, charges: Charges(ChargeIds.Grenade, 1)),
            [TemplateIds.Grenadier] = Soldier(
                TemplateIds.Grenadier, "Granadero", "GRN", WeaponIds.Cannon, hp: 6,
                abilities: new[] { AbilityIds.Shoot, AbilityIds.Launch, AbilityIds.Overwatch, AbilityIds.Reload, AbilityIds.Hunker }, charges: Charges(ChargeIds.Grenade, 2)),
            [TemplateIds.Sharpshooter] = Soldier(
                TemplateIds.Sharpshooter, "Francotirador", "FRT", WeaponIds.Sniper, secondary: WeaponIds.Pistol, aim: 70, squadsight: true,
                abilities: new[] { AbilityIds.Shoot, AbilityIds.Pistol, AbilityIds.Overwatch, AbilityIds.Grenade, AbilityIds.Reload, AbilityIds.Hunker }, charges: Charges(ChargeIds.Grenade, 1)),
            // A recruit with no class yet: rifle, a grenade and the basic actions (classes.ts specialises them).
            [TemplateIds.Rookie] = Soldier(
                TemplateIds.Rookie, "Recluta", "RCL", WeaponIds.Rifle,
                abilities: new[] { AbilityIds.Shoot, AbilityIds.Overwatch, AbilityIds.Grenade, AbilityIds.Reload, AbilityIds.Hunker }, charges: Charges(ChargeIds.Grenade, 1)),
            [TemplateIds.Specialist] = Soldier(
                TemplateIds.Specialist, "Especialista", "ESP", WeaponIds.Rifle,
                abilities: new[] { AbilityIds.Shoot, AbilityIds.Overwatch, AbilityIds.Grenade, AbilityIds.Reload, AbilityIds.Hunker }, charges: Charges(ChargeIds.Grenade, 1)),
            // The living: they take cover and shoot (ai.ts takeAlienTurn), and hear the noise.
            [TemplateIds.Trooper] = Alien(TemplateIds.Trooper, "Saqueador", "SAQ", WeaponIds.TrooperRifle, abilities: new[] { AbilityIds.Shoot, AbilityIds.Overwatch, AbilityIds.Reload, AbilityIds.Grenade }, charges: Charges(ChargeIds.Grenade, 1)),
            [TemplateIds.Officer] = Alien(TemplateIds.Officer, "Cabecilla", "CAB", WeaponIds.OfficerRifle, hp: 6, abilities: new[] { AbilityIds.Shoot, AbilityIds.Overwatch, AbilityIds.Reload }),
            [TemplateIds.Lancer] = Alien(TemplateIds.Lancer, "Cazador", "CAZ", WeaponIds.LancerRifle, meleeWeapon: WeaponIds.StunLance, hp: 4, abilities: new[] { AbilityIds.StunLance, AbilityIds.Shoot, AbilityIds.Reload }),
            // The dead (docs/FASE2.md): claws and teeth, no cover, the horde's AI (ai.ts takeZombieTurn).
            // Harder to put down (playtest 2026-10-15, #12): two more points of health each.
            // Playtest 16/10 #19 and #21: 3 of health; they move 80 % of what a soldier does (ZOMBIE_PACE, 28/10).
            [TemplateIds.Zombie] = Alien(TemplateIds.Zombie, "Zombi", "ZOM", WeaponIds.Claws, meleeWeapon: WeaponIds.Claws, hp: 3, aim: 60, defense: -10, mobility: SoldierMobility * ZombiePace, sight: 14, detection: 6, will: 100, undead: true, abilities: new[] { AbilityIds.Claw, AbilityIds.Smash }),
            [TemplateIds.Xenoid] = Alien(TemplateIds.Xenoid, "Corredor", "COR", WeaponIds.XenoPistol, meleeWeapon: WeaponIds.XenoPistol, hp: 7, aim: 60, mobility: 8, detection: 10, will: 100, undead: true, abilities: new[] { AbilityIds.Claw, AbilityIds.Smash }),
            [TemplateIds.Mec] = Alien(TemplateIds.Mec, "Acorazado", "ACO", WeaponIds.MecCannon, meleeWeapon: WeaponIds.MecCannon, hp: 9, armor: 1, aim: 60, mobility: SoldierMobility * ZombiePace, sight: 14, detection: 6, robotic: true, will: 100, undead: true, abilities: new[] { AbilityIds.Claw, AbilityIds.Smash }),
            // The Chillón doesn't bite: it screams (mindspin, noise 16) and raises the dead.
            [TemplateIds.Sectoid] = Alien(TemplateIds.Sectoid, "Chillón", "CHI", WeaponIds.SectoidPistol, hp: 7, mobility: 5, will: 100, undead: true, abilities: new[] { AbilityIds.Mindspin, AbilityIds.Reanimate }),
            // The Hinchado: slow; it bursts when it dies or reaches a soldier (rules.ts burst).
            [TemplateIds.Bloater] = Alien(TemplateIds.Bloater, "Hinchado", "HIN", WeaponIds.BloaterBurst, hp: 8, aim: 0, defense: -15, mobility: 3, sight: 12, detection: 5, will: 100, undead: true, abilities: new[] { AbilityIds.Burst }),
            // Objectives. The relay is an alien "unit" so every weapon can hit it; it never acts.
            [TemplateIds.Turret] = Soldier(TemplateIds.Turret, "Torreta", "TOR", WeaponIds.TurretGun, hp: 12, armor: 1, aim: 60, mobility: 0, sight: 14, will: 100, robotic: true, abilities: new string[0], charges: Charges()),
            [TemplateIds.Relay] = Alien(TemplateIds.Relay, "Alarma", "ALA", WeaponIds.TrooperRifle, hp: 8, armor: 1, aim: 0, mobility: 0, sight: 0, detection: 0, robotic: true, abilities: new string[0]),
            [TemplateIds.Vip] = Soldier(TemplateIds.Vip, "VIP", "VIP", WeaponIds.Pistol, hp: 4, aim: 50, will: 30, abilities: new[] { AbilityIds.Hunker }, charges: Charges()),
        };

        /// <summary>Default kits: each player gets a mix, a solo player gets all six.</summary>
        /// <remarks>Soldiers in a skirmish squad.</remarks>
        public const int SquadSize = 6;

        private static readonly HashSet<string> Attacks = new HashSet<string>
        {
            AbilityIds.Shoot, AbilityIds.Pistol, AbilityIds.Slash, AbilityIds.StunLance, AbilityIds.Claw, AbilityIds.RapidFire,
            AbilityIds.ChainShot, AbilityIds.AimedShot, AbilityIds.Rupture, AbilityIds.LightningHands, AbilityIds.Faceoff,
        };

        /// <summary>Abilities that fire a weapon (one roll to hit per shot).</summary>
        public static bool IsAttack(string ability)
        {
            return Attacks.Contains(ability);
        }

        /// <summary>Weapon used by an attack ability. A campaign soldier's are its own (Unit.arms): the primary, or the pistol without one; the knife without a machete.</summary>
        public static WeaponDef AttackWeapon(Unit u, string ability = AbilityIds.Shoot)
        {
            if (u.Arms != null)
            {
                switch (Abilities[ability].Weapon)
                {
                    case AbilityWeapons.Secondary:
                        return Weapons[WeaponIds.Pistol];
                    case AbilityWeapons.Melee:
                        return Weapons[!string.IsNullOrEmpty(u.Arms.Melee) ? FoundWeapons[u.Arms.Melee!].Weapon : WeaponIds.Knife];
                    default:
                        return Weapons[!string.IsNullOrEmpty(u.Arms.Primary) ? FoundWeapons[u.Arms.Primary!].Weapon : WeaponIds.Pistol];
                }
            }
            var t = Templates[u.Template];
            switch (Abilities[ability].Weapon)
            {
                case AbilityWeapons.Secondary:
                    return Weapons[t.Secondary ?? t.Weapon];
                case AbilityWeapons.Melee:
                    return Weapons[t.MeleeWeapon ?? t.Weapon];
                default:
                    return Weapons[t.Weapon];
            }
        }

        /// <summary>Rounds in a full clip of the weapon `ability` fires; 0, it never runs out. A campaign soldier's pistol counts them (docs/FASE8.md §A).</summary>
        public static int ClipOf(Unit u, string ability = AbilityIds.Shoot)
        {
            var w = AttackWeapon(u, ability);
            if (u.Arms != null && w.Id == WeaponIds.Pistol) return PistolClip;
            return w.Clip + (FiresPrimary(u, ability) && HasMod(u, WeaponModIds.ExtMag) ? ExtMag : 0);
        }

        /// <summary>Whether `ability` fires the pistol carried beside a primary weapon: it has its own rounds (Unit.pistolAmmo).</summary>
        public static bool FiresSidearm(Unit u, string ability)
        {
            return !string.IsNullOrEmpty(u.Arms?.Primary) && AttackWeapon(u, ability).Id == WeaponIds.Pistol;
        }

        /// <summary>Rounds left for `ability`'s weapon.</summary>
        public static int RoundsOf(Unit u, string ability = AbilityIds.Shoot)
        {
            return FiresSidearm(u, ability) ? (u.PistolAmmo ?? 0) : u.Ammo;
        }

        /// <summary>Whether `ability` can fire now: its weapon never runs out, or it has a round left.</summary>
        public static bool LoadedFor(Unit u, string ability = AbilityIds.Shoot)
        {
            // A broken weapon doesn't fire (docs/FASE9.md §5), loaded or not.
            if (FiresPrimary(u, ability) && ConditionOf(u) <= 0) return false;
            return ClipOf(u, ability) == 0 || RoundsOf(u, ability) > 0;
        }

        /// <summary>Reloading: the main gun if it's short of a full clip, otherwise the pistol carried beside it (docs/FASE8.md §A).</summary>
        public static void ReloadWeapon(Unit u)
        {
            var main = ClipOf(u, AbilityIds.Shoot);
            if (u.Ammo < main || string.IsNullOrEmpty(u.Arms?.Primary)) u.Ammo = main;
            else u.PistolAmmo = PistolClip;
        }

        /// <summary>Whether every gun the soldier carries is full.</summary>
        public static bool FullyLoaded(Unit u)
        {
            return u.Ammo >= ClipOf(u, AbilityIds.Shoot) && (string.IsNullOrEmpty(u.Arms?.Primary) || (u.PistolAmmo ?? 0) >= PistolClip);
        }

        /// <summary>Squadsight: the class's, and with arms of its own only with a fusil de precisión in hand (docs/FASE8.md §A).</summary>
        public static bool HasSquadsight(Unit u)
        {
            return Templates[u.Template].Squadsight && (u.Arms == null || AttackWeapon(u, AbilityIds.Shoot).Id == WeaponIds.Sniper);
        }

        /// <summary>
        /// Soldiers' names (Lore, 2026-10-13: people from everywhere, half women and
        /// half men), each with the sex the client picks a model of (Appearance.sex).
        /// </summary>
        public static readonly IReadOnlyList<SoldierName> SoldierNames = new[]
        {
            new SoldierName("Chioma Okafor", Sexes.F), new SoldierName("Kofi Mensah", Sexes.M), new SoldierName("Yuki Tanaka", Sexes.F),
            new SoldierName("Jonas Becker", Sexes.M), new SoldierName("Leila Mansour", Sexes.F), new SoldierName("Arjun Mehta", Sexes.M),
            new SoldierName("Sofia Marin", Sexes.F), new SoldierName("Dmitri Volkov", Sexes.M), new SoldierName("Priya Nair", Sexes.F),
            new SoldierName("Mateo Rojas", Sexes.M), new SoldierName("Anja Kowalska", Sexes.F), new SoldierName("Hiro Sato", Sexes.M),
            new SoldierName("Elena Petrova", Sexes.F), new SoldierName("Omar Saleh", Sexes.M), new SoldierName("Samira Benali", Sexes.F),
            new SoldierName("Liam O'Brien", Sexes.M), new SoldierName("Maya Cohen", Sexes.F), new SoldierName("Pierre Lambert", Sexes.M),
            new SoldierName("Chiara Rossi", Sexes.F), new SoldierName("Ivan Horvat", Sexes.M), new SoldierName("Freya Lund", Sexes.F),
            new SoldierName("Rafael Costa", Sexes.M), new SoldierName("Ana Ríos", Sexes.F), new SoldierName("Erik Nilsson", Sexes.M),
            new SoldierName("Zainab Bello", Sexes.F), new SoldierName("Samuel Adeyemi", Sexes.M), new SoldierName("Hana Novak", Sexes.F),
            new SoldierName("Wei Zhang", Sexes.M), new SoldierName("Lucía Ortega", Sexes.F), new SoldierName("Luca Bianchi", Sexes.M),
            new SoldierName("Ingrid Berg", Sexes.F), new SoldierName("Marcus Reid", Sexes.M), new SoldierName("Mariana Duarte", Sexes.F),
            new SoldierName("Tariq Rahimi", Sexes.M), new SoldierName("Aisha Diallo", Sexes.F), new SoldierName("Pablo Rey", Sexes.M),
        };

        /// <summary>
        /// Names older saves may hold, for sexOfName: the soldiers before 2026-10-13,
        /// those renamed on 2026-10-14 (they clashed with someone else's), and the
        /// relative of «Caras conocidas» retired that day, matched by first name.
        /// </summary>
        private static readonly IReadOnlyList<SoldierName> OldSoldierNames = new[]
        {
            new SoldierName("Ana Ríos", Sexes.F), new SoldierName("Luis Varela", Sexes.M), new SoldierName("Marta Soler", Sexes.F),
            new SoldierName("Iker Ortega", Sexes.M), new SoldierName("Nora Campos", Sexes.F), new SoldierName("Hugo Lázaro", Sexes.M),
            new SoldierName("Elena Vidal", Sexes.F), new SoldierName("Darío Pons", Sexes.M), new SoldierName("Sara Mena", Sexes.F),
            new SoldierName("Raúl Ibarra", Sexes.M), new SoldierName("Lucía Prat", Sexes.F), new SoldierName("Óscar Nieto", Sexes.M),
            new SoldierName("Irene Galán", Sexes.F), new SoldierName("Pablo Rey", Sexes.M), new SoldierName("Clara Bosch", Sexes.F),
            new SoldierName("Adrián Gil", Sexes.M), new SoldierName("Julia Font", Sexes.F), new SoldierName("Marcos Vela", Sexes.M),
            new SoldierName("Amara Okafor", Sexes.F), new SoldierName("Anja Kowalski", Sexes.F), new SoldierName("Nadia Benali", Sexes.F),
            new SoldierName("Pierre Dubois", Sexes.M), new SoldierName("Mariana Costa", Sexes.F), new SoldierName("Tariq Aziz", Sexes.M),
            new SoldierName("Kenji", Sexes.M),
        };

        /// <summary>
        /// The sex a name goes with: the whole name in SOLDIER_NAMES or the old list,
        /// or else its first word against their first names; undefined when nothing
        /// matches (a name the players chose).
        /// </summary>
        public static string? SexOfName(string name)
        {
            var all = SoldierNames.Concat(OldSoldierNames).ToList();
            var whole = all.FirstOrDefault((n) => n.Name == name);
            if (whole != null) return whole.Sex;
            // name.trim().split(/\s+/)[0]
            var trimmed = Js.Trim(name);
            var end = 0;
            while (end < trimmed.Length && !Js.IsSpace(trimmed[end])) end++;
            var first = trimmed.Substring(0, end);
            return all.FirstOrDefault((n) => n.Name.Split(' ')[0] == first)?.Sex;
        }

        // Violet and orange: apart from the interfaces' own blues and cyans, and from the aliens' red.
        public static readonly IReadOnlyList<string> PlayerColors = new[] { "#a678ff", "#ffa630" };

        // ------------------------------------------------------------ ranks & perks

        /// <summary>Every mod at 0; a new one each time, so it is a copy (`{ ...ZERO_MODS }`).</summary>
        public static UnitMods ZeroMods => new UnitMods();

        /// <summary>
        /// Whether `a` and `b` fight each other: the squad against everyone, and among
        /// the enemies the dead against the living (Arturo, 18/10 #47: «los saqueadores
        /// y los zombis se atacan entre sí»).
        /// </summary>
        public static bool HostileTo(Unit a, Unit b)
        {
            return HostileTo(a.Team, a.Template, b.Team, b.Template);
        }

        /// <summary>hostileTo on `Pick&lt;Unit, 'team' | 'template'&gt;`: a team and a template each.</summary>
        public static bool HostileTo(string aTeam, string aTemplate, string bTeam, string bTemplate)
        {
            if (aTeam != bTeam) return true;
            return aTeam == Teams.Alien && Templates[aTemplate].Undead != Templates[bTemplate].Undead;
        }

        /// <summary>
        /// Whether `slot` gives orders to what belongs to `owner` (a soldier, a weapon it carries): its own, and its
        /// partner's while the partner isn't connected (Arturo, 17/10: «si juego solo, debería poder usarlo solo»).
        /// Without `connected`, only its own. Shared units (owner null) are everyone's.
        /// </summary>
        public static bool CommandsFor(int slot, int? owner, IReadOnlyList<int>? connected = null)
        {
            return owner == null || owner == slot || (connected != null && !connected.Contains(owner.Value));
        }

        public static bool HasPerk(Unit u, string id)
        {
            return u.Perks.Contains(id);
        }

        /// <summary>`a` plus `b`, field by field, in a new UnitMods (`b` a Partial&lt;UnitMods&gt;: what it doesn't name is 0).</summary>
        public static UnitMods AddMods(UnitMods a, UnitMods b)
        {
            return new UnitMods
            {
                Aim = a.Aim + b.Aim,
                Crit = a.Crit + b.Crit,
                Defense = a.Defense + b.Defense,
                Mobility = a.Mobility + b.Mobility,
                Damage = a.Damage + b.Damage,
                MeleeDamage = a.MeleeDamage + b.MeleeDamage,
                PistolAim = a.PistolAim + b.PistolAim,
                CloseRangeAim = a.CloseRangeAim + b.CloseRangeAim,
                CloseRangeCrit = a.CloseRangeCrit + b.CloseRangeCrit,
                GrenadeDamage = a.GrenadeDamage + b.GrenadeDamage,
                GrenadeRadius = a.GrenadeRadius + b.GrenadeRadius,
                Heal = a.Heal + b.Heal,
                Aid = a.Aid + b.Aid,
                MeleeAim = a.MeleeAim + b.MeleeAim,
                FlankDamage = a.FlankDamage + b.FlankDamage,
                Shred = a.Shred + b.Shred,
                Will = a.Will + b.Will,
                Pierce = a.Pierce + b.Pierce,
                Dodge = a.Dodge + b.Dodge,
                CritDamage = a.CritDamage + b.CritDamage,
                MeleeCrit = a.MeleeCrit + b.MeleeCrit,
                PistolDamage = a.PistolDamage + b.PistolDamage,
                PistolCrit = a.PistolCrit + b.PistolCrit,
                CloseDamage = a.CloseDamage + b.CloseDamage,
                HeightAim = a.HeightAim + b.HeightAim,
                ReactionKeep = a.ReactionKeep + b.ReactionKeep,
                ReactionDefense = a.ReactionDefense + b.ReactionDefense,
                WatchDefense = a.WatchDefense + b.WatchDefense,
                SquadsightKeep = a.SquadsightKeep + b.SquadsightKeep,
                PairAim = a.PairAim + b.PairAim,
                PairDefense = a.PairDefense + b.PairDefense,
                SpotterAim = a.SpotterAim + b.SpotterAim,
                GrenadeShred = a.GrenadeShred + b.GrenadeShred,
                GrenadeRange = a.GrenadeRange + b.GrenadeRange,
                HoloAim = a.HoloAim + b.HoloAim,
                SuppressAim = a.SuppressAim + b.SuppressAim,
                Hack = a.Hack + b.Hack,
                PsiGuard = a.PsiGuard + b.PsiGuard,
                SlashDisorient = a.SlashDisorient + b.SlashDisorient,
                Sight = a.Sight + b.Sight,
                SmokeSight = a.SmokeSight + b.SmokeSight,
                CalmAura = a.CalmAura + b.CalmAura,
                GunAim = a.GunAim + b.GunAim,
                GunCrit = a.GunCrit + b.GunCrit,
                Quiet = a.Quiet + b.Quiet,
                StoneDamage = a.StoneDamage + b.StoneDamage,
                StoneNoise = a.StoneNoise + b.StoneNoise,
                Care = a.Care + b.Care,
            };
        }
    }
}
