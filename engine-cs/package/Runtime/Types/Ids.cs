#nullable enable
// types.ts: the ids and tags that are string unions in TypeScript. Each stays a `string` with the same values; these
// classes list them.
using System.Collections.Generic;

namespace Bastion.Engine
{
    public static class Teams
    {
        public const string Xcom = "xcom";
        public const string Alien = "alien";

        public static readonly IReadOnlyList<string> All = new[] { Xcom, Alien };
    }

    // Player seat in a co-op session.
    //   export type Slot = 0 | 1;
    // An `int` in C#.

    public static class ClassIds
    {
        public const string Assault = "assault";
        public const string Grenadier = "grenadier";
        public const string Sharpshooter = "sharpshooter";
        public const string Specialist = "specialist";

        public static readonly IReadOnlyList<string> All = new[] { Assault, Grenadier, Sharpshooter, Specialist };
    }

    /// <summary>The humans (trooper, officer, lancer) and the zombies (zombie, xenoid, mec, sectoid, bloater: `undead` in TEMPLATES).</summary>
    public static class AlienIds
    {
        public const string Trooper = "trooper";
        public const string Officer = "officer";
        public const string Xenoid = "xenoid";
        public const string Lancer = "lancer";
        public const string Mec = "mec";
        public const string Sectoid = "sectoid";
        public const string Zombie = "zombie";
        public const string Bloater = "bloater";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Trooper, Officer, Xenoid, Lancer, Mec, Sectoid, Zombie, Bloater,
        };
    }

    /// <summary>Mission objectives that are units: the alien relay to destroy and the VIP to extract.</summary>
    public static class ObjectiveIds
    {
        public const string Relay = "relay";
        public const string Vip = "vip";

        public static readonly IReadOnlyList<string> All = new[] { Relay, Vip };
    }

    /// <summary>The base's defences that are units (docs/FASE12.md §11.4): the turret, which shoots on its own.</summary>
    public static class DefenceIds
    {
        public const string Turret = "turret";

        public static readonly IReadOnlyList<string> All = new[] { Turret };
    }

    /// <summary>'rookie': a recruit with no class yet (classes.ts), a rifle and the basic actions.</summary>
    /// <summary>What a zombie wore, when it was a uniform (Unit.Outfit).</summary>
    public static class Outfits
    {
        public const string Police = "police";
        public const string Military = "military";
        /// <summary>A hospital gown: the dead in plain clothes that start inside a clinic or a hospital (Setup; Arturo, 9/11).</summary>
        public const string Patient = "patient";
    }

    public static class TemplateIds
    {
        // ClassId.
        public const string Assault = ClassIds.Assault;
        public const string Grenadier = ClassIds.Grenadier;
        public const string Sharpshooter = ClassIds.Sharpshooter;
        public const string Specialist = ClassIds.Specialist;
        public const string Rookie = "rookie";
        // AlienId.
        public const string Trooper = AlienIds.Trooper;
        public const string Officer = AlienIds.Officer;
        public const string Xenoid = AlienIds.Xenoid;
        public const string Lancer = AlienIds.Lancer;
        public const string Mec = AlienIds.Mec;
        public const string Sectoid = AlienIds.Sectoid;
        public const string Zombie = AlienIds.Zombie;
        public const string Bloater = AlienIds.Bloater;
        // ObjectiveId.
        public const string Relay = ObjectiveIds.Relay;
        public const string Vip = ObjectiveIds.Vip;
        // DefenceId.
        public const string Turret = DefenceIds.Turret;

        public static readonly IReadOnlyList<string> All = new[]
        {
            Assault, Grenadier, Sharpshooter, Specialist, Rookie, Trooper, Officer, Xenoid, Lancer, Mec, Sectoid, Zombie,
            Bloater, Relay, Vip, Turret,
        };
    }

    /// <summary>Head pieces a soldier can wear (barracks customization).</summary>
    public static class HeadIds
    {
        public const string Helmet = "helmet";
        public const string Hood = "hood";
        public const string Beret = "beret";
        public const string Cap = "cap";
        public const string Mask = "mask";
        public const string Bare = "bare";
        // 0.12.15 (Arturo, 9/11: «Mochilas y cascos»).
        public const string VisorHelmet = "visorHelmet";
        public const string EarflapHat = "earflapHat";
        public const string TwoToneCap = "twoToneCap";
        public const string GoggleHelmet = "goggleHelmet";
        public const string RiotHelmet = "riotHelmet";
        public const string PlainCap = "plainCap";
        public const string Bandana = "bandana";
        public const string ScarfHood = "scarfHood";
        public const string BucketHat = "bucketHat";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Helmet, Hood, Beret, Cap, Mask, Bare, VisorHelmet, EarflapHat, TwoToneCap, GoggleHelmet, RiotHelmet, PlainCap, Bandana,
            ScarfHood, BucketHat,
        };
    }

    /// <summary>Body builds.</summary>
    public static class BuildIds
    {
        public const string Slim = "slim";
        public const string Normal = "normal";
        public const string Heavy = "heavy";

        public static readonly IReadOnlyList<string> All = new[] { Slim, Normal, Heavy };
    }

    /// <summary>Armour pieces the barracks paints one by one (appearance.ts).</summary>
    public static class ArmorParts
    {
        public const string Helmet = "helmet";
        public const string Torso = "torso";
        public const string Arms = "arms";
        public const string Legs = "legs";

        public static readonly IReadOnlyList<string> All = new[] { Helmet, Torso, Arms, Legs };
    }

    public static class AbilityIds
    {
        public const string Shoot = "shoot";
        public const string Pistol = "pistol";
        public const string Slash = "slash";
        public const string StunLance = "stunLance";
        public const string Overwatch = "overwatch";
        public const string Hunker = "hunker";
        public const string Reload = "reload";
        public const string Grenade = "grenade";
        public const string Launch = "launch";
        public const string Missiles = "missiles";
        public const string Evac = "evac";
        public const string Hack = "hack";
        /// <summary>Opening a special loot box beside the soldier (docs/FASE8.md §B; before, any furniture or car).</summary>
        public const string Search = "search";
        /// <summary>Searching the room the soldier is in, once for the squad (docs/FASE8.md §B).</summary>
        public const string SearchRoom = "searchRoom";
        /// <summary>Opening or closing a door beside the soldier without moving (playtest 2026-10-13): free.</summary>
        public const string OpenDoor = "openDoor";
        public const string CloseDoor = "closeDoor";
        // Granted by perks.
        public const string RunAndGun = "runAndGun";
        public const string RapidFire = "rapidFire";
        public const string Suppression = "suppression";
        public const string ChainShot = "chainShot";
        public const string Rupture = "rupture";
        public const string AimedShot = "aimedShot";
        public const string LightningHands = "lightningHands";
        public const string Faceoff = "faceoff";
        public const string KillZone = "killZone";
        public const string Revival = "revival";
        public const string Restoration = "restoration";
        // Fase 9's talents (skills.ts).
        /// <summary>Hacerse sitio: a Slash at every enemy alongside.</summary>
        public const string ElbowRoom = "elbowRoom";
        /// <summary>Apaños: 40 of condition back to one's own weapon or an adjacent ally's.</summary>
        public const string PatchUp = "patchUp";
        // Fase 10 (docs/FASE10.md §4).
        /// <summary>A stone, always at hand: heard where it lands, 1 of damage to whoever it hits. Doesn't end the turn.</summary>
        public const string Stone = "stone";
        /// <summary>Overwatch with the blade: strikes the first enemy that comes alongside.</summary>
        public const string OverwatchMelee = "overwatchMelee";
        // Alien powers.
        public const string Claw = "claw";
        public const string Mindspin = "mindspin";
        public const string Reanimate = "reanimate";
        /// <summary>The Hinchado bursts.</summary>
        public const string Burst = "burst";
        /// <summary>In a siege (docs/FASE12.md §4), the dead beat on a door or a barricade beside them.</summary>
        public const string Smash = "smash";
        // Granted by equipment.
        public const string Smoke = "smoke";
        public const string Flashbang = "flashbang";
        public const string FirstAid = "firstAid";
        /// <summary>The cóctel molotov (Arturo, 1/11): thrown, it sets the ground round where it lands on fire.</summary>
        public const string Molotov = "molotov";
        /// <summary>Esconderse (Ana's #1 of 1/11): crouched out of sight till the soldier's next turn; Cubrirse is Hunker.</summary>
        public const string Hide = "hide";
        /// <summary>The crowbar's (Arturo, 2/11): the boards off a window or a door beside it (Abilities.BoardedBeside).</summary>
        public const string Pry = "pry";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Shoot, Pistol, Slash, StunLance, Overwatch, Hunker, Reload, Grenade, Launch, Missiles, Evac, Hack, Search,
            SearchRoom, OpenDoor, CloseDoor, RunAndGun, RapidFire, Suppression, ChainShot, Rupture, AimedShot,
            LightningHands, Faceoff, KillZone, Revival, Restoration, ElbowRoom, PatchUp, Stone, OverwatchMelee, Claw,
            Mindspin, Reanimate, Burst, Smash, Smoke, Flashbang, FirstAid, Molotov, Hide, Pry,
        };
    }

    /// <summary>Abilities that roll to hit against a single unit.</summary>
    public static class AttackAbilities
    {
        public const string Shoot = "shoot";
        public const string Pistol = "pistol";
        public const string Slash = "slash";
        public const string StunLance = "stunLance";
        public const string Claw = "claw";
        public const string RapidFire = "rapidFire";
        public const string ChainShot = "chainShot";
        public const string AimedShot = "aimedShot";
        public const string Rupture = "rupture";
        public const string LightningHands = "lightningHands";
        public const string Faceoff = "faceoff";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Shoot, Pistol, Slash, StunLance, Claw, RapidFire, ChainShot, AimedShot, Rupture, LightningHands, Faceoff,
        };
    }

    /// <summary>Area abilities thrown or fired at a tile.</summary>
    public static class BlastAbilities
    {
        public const string Grenade = "grenade";
        public const string Launch = "launch";
        public const string Missiles = "missiles";
        public const string Smoke = "smoke";
        public const string Flashbang = "flashbang";
        public const string Burst = "burst";
        public const string Molotov = "molotov";

        public static readonly IReadOnlyList<string> All = new[] { Grenade, Launch, Missiles, Smoke, Flashbang, Burst, Molotov };
    }

    /// <summary>Limited-use items.</summary>
    public static class ChargeIds
    {
        public const string Grenade = "grenade";
        public const string Missiles = "missiles";
        public const string Smoke = "smoke";
        public const string Flashbang = "flashbang";
        public const string FirstAid = "firstAid";
        // Fase 9's talents with uses per mission (skills.ts).
        public const string Restoration = "restoration";
        public const string PatchUp = "patchUp";
        /// <summary>The chainsaw's: a blow each (Content.ChainsawFuel).</summary>
        public const string Fuel = "fuel";
        public const string Molotov = "molotov";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Grenade, Missiles, Smoke, Flashbang, FirstAid, Restoration, PatchUp, Fuel, Molotov,
        };
    }

    /// <summary>Equipment fabricated in the base: one utility item and one ammo type per soldier.</summary>
    public static class ItemIds
    {
        public const string FragGrenade = "fragGrenade";
        public const string SmokeGrenade = "smokeGrenade";
        public const string Flashbang = "flashbang";
        public const string FirstAid = "firstAid";
        public const string NanoVest = "nanoVest";
        public const string MindShield = "mindShield";
        public const string ApRounds = "apRounds";
        public const string TracerRounds = "tracerRounds";
        /// <summary>Before the grenades (Arturo, 1/11): from the Bastión's first level.</summary>
        public const string Molotov = "molotov";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Molotov, FragGrenade, SmokeGrenade, Flashbang, FirstAid, NanoVest, MindShield, ApRounds, TracerRounds,
        };
    }

    public static class ItemSlots
    {
        public const string Utility = "utility";
        public const string Ammo = "ammo";

        public static readonly IReadOnlyList<string> All = new[] { Utility, Ammo };
    }

    /// <summary>The Armería's weapons (docs/FASE8.md §A, content.ts FOUND_WEAPONS): the plain ones, and the good ones found searching (docs/FASE4.md).</summary>
    public static class FoundWeaponIds
    {
        public const string AssaultRifle = "assaultRifle";
        public const string PumpShotgun = "pumpShotgun";
        public const string PrecisionRifle = "precisionRifle";
        public const string MachineGun = "machineGun";
        public const string Machete = "machete";
        public const string HuntingRifle = "huntingRifle";
        public const string SawnOff = "sawnOff";
        public const string ScopedRifle = "scopedRifle";
        public const string LightMachineGun = "lightMachineGun";
        public const string Pan = "pan";
        public const string Bat = "bat";
        public const string Crowbar = "crowbar";
        public const string Chainsaw = "chainsaw";
        public const string Katana = "katana";
        public const string Longsword = "longsword";
        public const string Rake = "rake";
        public const string Stick = "stick";
        /// <summary>The pistol at the side, the Armería's since 1/11 (Arturo: nobody starts with one); see Arms.Pistol.</summary>
        public const string Pistol = "pistol";

        public static readonly IReadOnlyList<string> All = new[]
        {
            AssaultRifle, PumpShotgun, PrecisionRifle, MachineGun, Machete, HuntingRifle, SawnOff, ScopedRifle,
            LightMachineGun, Pan, Bat, Crowbar, Chainsaw, Pistol, Katana, Longsword, Rake, Stick,
        };
    }

    /// <summary>What can be mounted on a weapon (docs/FASE9.md §5), two at most: Mira, Culata, Cargador ampliado, Silenciador.</summary>
    public static class WeaponModIds
    {
        public const string Scope = "scope";
        public const string Stock = "stock";
        public const string ExtMag = "extMag";
        public const string Silencer = "silencer";

        public static readonly IReadOnlyList<string> All = new[] { Scope, Stock, ExtMag, Silencer };
    }

    /// <summary>What a successful Mindspin does to its victim ('controlled' only in missions saved before the zombies: the Chillón's scream never takes control).</summary>
    public static class PsiEffects
    {
        public const string Disoriented = "disoriented";
        public const string Panicked = "panicked";
        public const string Controlled = "controlled";

        public static readonly IReadOnlyList<string> All = new[] { Disoriented, Panicked, Controlled };
    }

    public static class ShotResults
    {
        public const string Miss = "miss";
        public const string Graze = "graze";
        public const string Hit = "hit";
        public const string Crit = "crit";

        public static readonly IReadOnlyList<string> All = new[] { Miss, Graze, Hit, Crit };
    }

    /// <summary>
    /// Why the squad lost its concealment (the client explains it): an alien saw a
    /// soldier walk into its detection range (`spotted`) or a patrol walked up to
    /// one (`patrol`); a soldier attacked or set off an explosive; a failed hack
    /// raised the alarm; reinforcements landed.
    /// </summary>
    public static class ConcealReasons
    {
        public const string Spotted = "spotted";
        public const string Patrol = "patrol";
        public const string Attack = "attack";
        public const string Explosive = "explosive";
        public const string Alarm = "alarm";
        public const string Reinforcements = "reinforcements";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Spotted, Patrol, Attack, Explosive, Alarm, Reinforcements,
        };
    }

    /// <summary>
    /// Why a pod came alive: a soldier came into its sight (`sighted`), it ran into
    /// the squad on patrol or it was attacked. `noise` only in older saves: noise
    /// no longer wakes a pod, it sends it to look (`Unit.heard`).
    /// </summary>
    public static class PodCauses
    {
        public const string Sighted = "sighted";
        public const string Patrol = "patrol";
        public const string Attacked = "attacked";
        public const string Noise = "noise";

        public static readonly IReadOnlyList<string> All = new[] { Sighted, Patrol, Attacked, Noise };
    }

    public static class Outcomes
    {
        public const string Victory = "victory";
        public const string Defeat = "defeat";

        public static readonly IReadOnlyList<string> All = new[] { Victory, Defeat };
    }

    public static class EndReasons
    {
        public const string Objective = "objective";
        public const string Wiped = "wiped";
        public const string Timer = "timer";
        public const string Abandoned = "abandoned";
        public const string ObjectiveLost = "objectiveLost";

        public static readonly IReadOnlyList<string> All = new[] { Objective, Wiped, Timer, Abandoned, ObjectiveLost };
    }

    /// <summary>
    /// elimination: kill every hostile.
    /// recovery:    pick up the data and extract it.
    /// sabotage:    destroy the alien relay before the timer runs out.
    /// hack:        hack the terminal before the timer runs out (failures raise the alarm).
    /// rescue:      reach the VIP and extract them alive.
    /// scavenge:    «Saqueo»: search what you can and get out by the evac; no timer, but the
    ///              horde comes in by the edges, sooner the more noise the squad makes.
    /// </summary>
    public static class MissionKinds
    {
        public const string Elimination = "elimination";
        public const string Recovery = "recovery";
        public const string Sabotage = "sabotage";
        public const string Hack = "hack";
        public const string Rescue = "rescue";
        public const string Scavenge = "scavenge";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Elimination, Recovery, Sabotage, Hack, Rescue, Scavenge,
        };
    }

    /// <summary>
    /// Behaviours the rules know by name: a soldier gets them from the cells of its
    /// hex board (board.ts `perk`), and the active ones bring their ability
    /// (board.ts PERK_ABILITY).
    /// </summary>
    public static class PerkIds
    {
        // Shooting.
        public const string AimedShot = "aimedShot";
        public const string RapidFire = "rapidFire";
        public const string ChainShot = "chainShot";
        public const string GuardCrit = "guardCrit";
        public const string DeathFromAbove = "deathFromAbove";
        public const string LightningHands = "lightningHands";
        public const string Serenity = "serenity";
        public const string CoveringFire = "coveringFire";
        public const string KillZone = "killZone";
        public const string Faceoff = "faceoff";
        public const string Rupture = "rupture";
        public const string Suppression = "suppression";
        public const string HoloTargeting = "holoTargeting";
        // Melee.
        public const string ElbowRoom = "elbowRoom";
        public const string Implacable = "implacable";
        public const string BladeMaster = "bladeMaster";
        /// <summary>Muro de acero: melee overwatch strikes every enemy that comes alongside, once each.</summary>
        public const string BladeWall = "bladeWall";
        /// <summary>Segundo aliento: a melee kill heals 1.</summary>
        public const string SecondWind = "secondWind";
        // Throwing.
        public const string Salvo = "salvo";
        public const string GoodArm = "goodArm";
        public const string SafeBlast = "safeBlast";
        /// <summary>Piedras: the stone is the board's now, no longer everyone's (Ana's notes, 29/10).</summary>
        public const string Stones = "stones";
        // Medicine.
        public const string RevivalProtocol = "revivalProtocol";
        public const string Bridge = "bridge";
        public const string LightHands = "lightHands";
        public const string Restoration = "restoration";
        // Searching.
        public const string PatchUp = "patchUp";
        // Fitness.
        public const string RunAndGun = "runAndGun";
        public const string Shadowstep = "shadowstep";

        public static readonly IReadOnlyList<string> All = new[]
        {
            AimedShot, RapidFire, ChainShot, GuardCrit, DeathFromAbove, LightningHands, Serenity, CoveringFire,
            KillZone, Faceoff, Rupture, Suppression, HoloTargeting, ElbowRoom, Implacable, BladeMaster, BladeWall,
            SecondWind, Salvo, GoodArm, SafeBlast, Stones, RevivalProtocol, Bridge, LightHands, Restoration, PatchUp,
            RunAndGun, Shadowstep,
        };
    }
}
