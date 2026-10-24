#nullable enable
// progression.ts: campaign progression. The Bastion's level is bought, with suministros and
// missions flown (Arturo, 18/10 #40), and each level unlocks technology,
// accessories and the base's pieces. Weapons and armour are never bought: the
// level opens what the Taller can make (CRAFTS, docs/FASE4.md), the armour is
// for everyone, and some levels bring weapons to the Armería (LEVEL_ARMS).
// campaign.ts is not ported yet: these functions take what they read of a campaign (`level`, `techs`, `crafted`,
// `foundWeapons`, a soldier's `id`) as arguments, not a CampaignState; FoundWeapon is here until campaign.ts is.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>TechId.</summary>
    public static class TechIds
    {
        public const string MagWeapons = "magWeapons";
        public const string PlateArmor = "plateArmor";
        public const string PlasmaGrenades = "plasmaGrenades";
        public const string Encryption = "encryption";
        public const string PlasmaWeapons = "plasmaWeapons";
        public const string NanoMedkit = "nanoMedkit";
        public const string PredatorArmor = "predatorArmor";
        public const string Signal = "signal";

        /// <summary>Also `Object.values(TECHS)`'s order.</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            MagWeapons, PlateArmor, PlasmaGrenades, Encryption, PlasmaWeapons, NanoMedkit, PredatorArmor, Signal,
        };
    }

    public sealed class TechDef
    {
        public string Id { get; set; } = ""; // TechIds
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        /// <summary>Bastion level that unlocks it.</summary>
        public int Level { get; set; }
    }

    /// <summary>CraftId.</summary>
    public static class CraftIds
    {
        public const string Armor2 = "armor2";
        public const string Armor3 = "armor3";
        public const string MakeRifle = "makeRifle";
        public const string MakeShotgun = "makeShotgun";
        public const string MakeSniper = "makeSniper";
        public const string MakeMachineGun = "makeMachineGun";
        public const string MakeMachete = "makeMachete";
        public const string Ammo1 = "ammo1";
        public const string Ammo2 = "ammo2";
        public const string Ammo3 = "ammo3";
        public const string MakeStock = "makeStock";
        public const string MakeSilencer = "makeSilencer";
        public const string MakeScope = "makeScope";
        public const string MakeExtMag = "makeExtMag";

        /// <summary>In the type's order; CRAFTS' keys (Object.keys) go in Progression.CraftOrder's.</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            Armor2, Armor3, MakeRifle, MakeShotgun, MakeSniper, MakeMachineGun, MakeMachete, Ammo1, Ammo2, Ammo3, MakeStock,
            MakeSilencer, MakeScope, MakeExtMag,
        };
    }

    /// <summary>CraftDef.kind: `'armor' | 'arms' | 'ammo' | 'mod'`.</summary>
    public static class CraftKinds
    {
        public const string Armor = "armor";
        public const string Arms = "arms";
        public const string Ammo = "ammo";
        public const string Mod = "mod";

        public static readonly IReadOnlyList<string> All = new[] { Armor, Arms, Ammo, Mod };
    }

    public sealed class CraftDef
    {
        public string Id { get; set; } = ""; // CraftIds
        public string Name { get; set; } = "";
        /// <summary>As the register tells it, with its article.</summary>
        public string The { get; set; } = "";
        /// <summary>An armour tier for everyone, a weapon for the Armería (made again and again), one more spare magazine for everyone (docs/FASE8.md §A), or an improvement for a weapon (docs/FASE9.md §5).</summary>
        public string Kind { get; set; } = ""; // CraftKinds
        /// <summary>The tier it makes ('armor').</summary>
        public int? Tier { get; set; } // 2 | 3
        /// <summary>The technology (the Bastion's level) that allows it.</summary>
        public string? Tech { get; set; } // TechIds
        /// <summary>The Bastion's level it needs, when no technology says it.</summary>
        public int? Level { get; set; }
        /// <summary>The weapon it makes ('arms').</summary>
        public string? Makes { get; set; } // FoundWeaponIds
        /// <summary>The improvement it makes ('mod'), for the Armería.</summary>
        public string? MakesMod { get; set; } // WeaponModIds
        /// <summary>Level of the Taller it needs.</summary>
        public int Workshop { get; set; }
        /// <summary>What has to be made before it.</summary>
        public string? After { get; set; } // CraftIds
        /// <summary>Suministros, parts and days of work (a crew's).</summary>
        public int Cost { get; set; }
        public int Parts { get; set; }
        public int Days { get; set; }
    }

    /// <summary>An element of ARMOR_TIERS: `{ name: string; hp: number; armor: number }`.</summary>
    public sealed class ArmorTierDef
    {
        public string Name { get; set; } = "";
        public int Hp { get; set; }
        public int Armor { get; set; }
    }

    /// <summary>A value of BASE_UNLOCKS, or of levelUnlocks' `facilities`: `{ name: string; level: number; building?: boolean }`.</summary>
    public sealed class BaseUnlock
    {
        public string Name { get; set; } = "";
        public int Level { get; set; }
        /// <summary>It rises levels (2 and 3); missing, as if it did.</summary>
        public bool? Building { get; set; }
    }

    // TODO(port): campaign.FoundWeapon: campaign.ts's type, here until campaign.ts is ported (then it moves there).
    /// <summary>A weapon of the Armería (docs/FASE8.md §A).</summary>
    public sealed class FoundWeapon
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = ""; // FoundWeaponIds
        /// <summary>The soldier who carries it; null, on the rack. Lost with them if they fall.</summary>
        public string? Holder { get; set; }
        /// <summary>0 to 100 (docs/FASE9.md §5); absent, 100. Below 30 it is «Gastada»; at 0, «Rota».</summary>
        public double? Condition { get; set; }
        /// <summary>What is mounted on it, MOD_SLOTS at most.</summary>
        public List<string>? Mods { get; set; } // WeaponModIds
    }

    public static class Progression
    {
        public static readonly IReadOnlyDictionary<string, TechDef> Techs = new Dictionary<string, TechDef>
        {
            [TechIds.MagWeapons] = new TechDef { Id = TechIds.MagWeapons, Name = "Culatas y silenciadores", Description = "Teo puede hacer culatas y silenciadores en el Taller, con piezas, para montarlos en la Armería.", Level = 3 },
            [TechIds.PlateArmor] = new TechDef { Id = TechIds.PlateArmor, Name = "Armadura de placas", Description = "El Taller puede hacer armadura de nivel 2 (+2 de salud), con piezas: para todos.", Level = 4 },
            [TechIds.PlasmaGrenades] = new TechDef { Id = TechIds.PlasmaGrenades, Name = "Granadas de clavos", Description = "Clavos y tornillos en la carga: +2 de daño en todas las granadas.", Level = 5 },
            [TechIds.Encryption] = new TechDef { Id = TechIds.Encryption, Name = "Escáner de radio", Description = "Oír las frecuencias de los Cuervos sin que nos oigan: +20 a arreglar equipos y una zona contactable más.", Level = 5 },
            [TechIds.PlasmaWeapons] = new TechDef { Id = TechIds.PlasmaWeapons, Name = "Miras y cargadores ampliados", Description = "Lo que quedaba en los cuarteles sirve de modelo: el Taller puede hacer miras y cargadores ampliados, con piezas, para montarlos en la Armería.", Level = 6 },
            [TechIds.NanoMedkit] = new TechDef { Id = TechIds.NanoMedkit, Name = "Antibióticos", Description = "Amoxicilina y suturas de verdad: las curas sanan +2 y los botiquines tienen un uso más.", Level = 6 },
            [TechIds.PredatorArmor] = new TechDef { Id = TechIds.PredatorArmor, Name = "Equipo antidisturbios", Description = "El Taller, a nivel 2, puede hacer armadura de nivel 3 (+4 de salud y +1 de blindaje) para todos.", Level = 7 },
            [TechIds.Signal] = new TechDef { Id = TechIds.Signal, Name = "La sirena en La Lata", Description = "La sirena de los Cuervos, atornillada al techo de La Lata para llevarse a la Marea: desbloquea la misión final.", Level = 8 },
        };

        /// <summary>
        /// What raising the Bastion to each level asks (Arturo, 18/10 #40: «se va
        /// upgradeando a mano, comprando»; «suministros y misiones»): the suministros
        /// it costs, and the missions flown before it can be. Index 0 is level 1.
        /// </summary>
        public static readonly IReadOnlyList<int> LevelCost = new[] { 0, 100, 160, 240, 330, 440, 560, 700 };
        public static readonly IReadOnlyList<int> LevelMissions = new[] { 0, 2, 4, 6, 9, 12, 16, 20 };
        public static readonly int MaxLevel = LevelCost.Count;

        /// <summary>The Bastion's level, bought (`level`; migrateCampaign gives one to campaigns from before version 21).</summary>
        /// <remarks>`c: Pick&lt;CampaignState, 'xp' | 'level'&gt;`: it reads only `level`.</remarks>
        public static int BastionLevel(int? level)
        {
            return level ?? 1;
        }

        /// <summary>Unlocked by the Bastion's level, or granted earlier by a side quest (`CampaignState.techs`).</summary>
        public static bool HasTech(int? level, IReadOnlyList<string>? techs, string id)
        {
            return (techs != null && techs.Contains(id)) || BastionLevel(level) >= Techs[id].Level;
        }

        public static bool ItemUnlocked(int? level, string id)
        {
            return BastionLevel(level) >= Content.Items[id].Level;
        }

        // ------------------------------------------------------------------ tiers

        // The armour's tiers (ARMOR_TIERS), `Tier = 1 | 2 | 3`: an int here; weapons have none since Fase 9 (docs/FASE9.md §5).

        // ----------------------------------------------------------------- crafts

        /// <summary>What the Taller makes (docs/FASE4.md): tiers 2 and 3 of weapons and armour, for everyone. Names are placeholders for Lore.</summary>
        public static readonly IReadOnlyDictionary<string, CraftDef> Crafts = new Dictionary<string, CraftDef>
        {
            [CraftIds.Armor2] = new CraftDef { Id = CraftIds.Armor2, Name = "Armadura de nivel 2", The = "la armadura de nivel 2", Kind = CraftKinds.Armor, Tier = 2, Tech = TechIds.PlateArmor, Workshop = 1, After = null, Cost = 60, Parts = 4, Days = 3 },
            [CraftIds.Armor3] = new CraftDef { Id = CraftIds.Armor3, Name = "Armadura de nivel 3", The = "la armadura de nivel 3", Kind = CraftKinds.Armor, Tier = 3, Tech = TechIds.PredatorArmor, Workshop = 2, After = CraftIds.Armor2, Cost = 120, Parts = 8, Days = 5 },
            // The weapons from zero (docs/FASE8.md §A): one at a time, for the Armería.
            [CraftIds.MakeRifle] = new CraftDef { Id = CraftIds.MakeRifle, Name = "Fusil de asalto", The = "un fusil de asalto", Kind = CraftKinds.Arms, Makes = FoundWeaponIds.AssaultRifle, Tech = null, Workshop = 1, After = null, Cost = 30, Parts = 3, Days = 2 },
            [CraftIds.MakeShotgun] = new CraftDef { Id = CraftIds.MakeShotgun, Name = "Escopeta", The = "una escopeta", Kind = CraftKinds.Arms, Makes = FoundWeaponIds.PumpShotgun, Tech = null, Workshop = 1, After = null, Cost = 30, Parts = 3, Days = 2 },
            [CraftIds.MakeMachete] = new CraftDef { Id = CraftIds.MakeMachete, Name = "Machete", The = "un machete", Kind = CraftKinds.Arms, Makes = FoundWeaponIds.Machete, Tech = null, Level = 2, Workshop = 1, After = null, Cost = 15, Parts = 2, Days = 1 },
            [CraftIds.MakeSniper] = new CraftDef { Id = CraftIds.MakeSniper, Name = "Fusil de precisión", The = "un fusil de precisión", Kind = CraftKinds.Arms, Makes = FoundWeaponIds.PrecisionRifle, Tech = null, Level = 3, Workshop = 1, After = null, Cost = 45, Parts = 5, Days = 3 },
            [CraftIds.MakeMachineGun] = new CraftDef { Id = CraftIds.MakeMachineGun, Name = "Ametralladora", The = "una ametralladora", Kind = CraftKinds.Arms, Makes = FoundWeaponIds.MachineGun, Tech = null, Level = 4, Workshop = 1, After = null, Cost = 45, Parts = 5, Days = 3 },
            // Munition: each one more spare magazine for every soldier who sets out (docs/FASE8.md §A).
            [CraftIds.Ammo1] = new CraftDef { Id = CraftIds.Ammo1, Name = "Primeros cargadores", The = "la primera tanda de cargadores", Kind = CraftKinds.Ammo, Tech = null, Workshop = 1, After = null, Cost = 25, Parts = 2, Days = 2 },
            [CraftIds.Ammo2] = new CraftDef { Id = CraftIds.Ammo2, Name = "Más cargadores", The = "la segunda tanda de cargadores", Kind = CraftKinds.Ammo, Tech = null, Level = 3, Workshop = 1, After = CraftIds.Ammo1, Cost = 40, Parts = 4, Days = 3 },
            [CraftIds.Ammo3] = new CraftDef { Id = CraftIds.Ammo3, Name = "Cargadores de sobra", The = "la tercera tanda de cargadores", Kind = CraftKinds.Ammo, Tech = null, Level = 5, Workshop = 2, After = CraftIds.Ammo2, Cost = 60, Parts = 6, Days = 4 },
            // Improvements for the weapons (docs/FASE9.md §5), one at a time: «Armas de taller» and «Armas militares» open them.
            [CraftIds.MakeStock] = new CraftDef { Id = CraftIds.MakeStock, Name = "Culata", The = "una culata", Kind = CraftKinds.Mod, MakesMod = WeaponModIds.Stock, Tech = TechIds.MagWeapons, Workshop = 1, After = null, Cost = 20, Parts = 2, Days = 1 },
            [CraftIds.MakeSilencer] = new CraftDef { Id = CraftIds.MakeSilencer, Name = "Silenciador", The = "un silenciador", Kind = CraftKinds.Mod, MakesMod = WeaponModIds.Silencer, Tech = TechIds.MagWeapons, Workshop = 1, After = null, Cost = 20, Parts = 2, Days = 1 },
            [CraftIds.MakeScope] = new CraftDef { Id = CraftIds.MakeScope, Name = "Mira", The = "una mira", Kind = CraftKinds.Mod, MakesMod = WeaponModIds.Scope, Tech = TechIds.PlasmaWeapons, Workshop = 1, After = null, Cost = 30, Parts = 3, Days = 2 },
            [CraftIds.MakeExtMag] = new CraftDef { Id = CraftIds.MakeExtMag, Name = "Cargador ampliado", The = "un cargador ampliado", Kind = CraftKinds.Mod, MakesMod = WeaponModIds.ExtMag, Tech = TechIds.PlasmaWeapons, Workshop = 1, After = null, Cost = 30, Parts = 3, Days = 2 },
        };

        /// <summary>`Object.keys(CRAFTS)`'s order: its keys as progression.ts writes them (the machete before the precision rifle).</summary>
        public static readonly IReadOnlyList<string> CraftOrder = new[]
        {
            CraftIds.Armor2, CraftIds.Armor3, CraftIds.MakeRifle, CraftIds.MakeShotgun, CraftIds.MakeMachete, CraftIds.MakeSniper,
            CraftIds.MakeMachineGun, CraftIds.Ammo1, CraftIds.Ammo2, CraftIds.Ammo3, CraftIds.MakeStock, CraftIds.MakeSilencer,
            CraftIds.MakeScope, CraftIds.MakeExtMag,
        };

        /// <summary>What the Bastion's level brings to the Armería (docs/FASE8.md §A), by the level reached.</summary>
        public static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> LevelArms = new Dictionary<int, IReadOnlyList<string>>
        {
            [2] = new[] { FoundWeaponIds.AssaultRifle, FoundWeaponIds.PumpShotgun },
            [3] = new[] { FoundWeaponIds.PrecisionRifle, FoundWeaponIds.Machete },
            [4] = new[] { FoundWeaponIds.MachineGun },
        };

        // What the Armería holds: the Taller's work and the weapons found.
        //   type Armoury = { crafted?: readonly CraftId[]; foundWeapons?: readonly FoundWeapon[] };
        // In C#, its two fields are passed as they are needed: `crafted`, `foundWeapons`.

        /// <summary>Spare magazines every soldier sets out with: one per munition the Taller has made.</summary>
        public static int SpareMags(IReadOnlyList<string>? crafted)
        {
            return new[] { CraftIds.Ammo1, CraftIds.Ammo2, CraftIds.Ammo3 }.Count((id) => HasCrafted(crafted, id));
        }

        public static bool HasCrafted(IReadOnlyList<string>? crafted, string id)
        {
            return crafted != null && crafted.Contains(id);
        }

        /// <summary>The project that makes armour of `tier`.</summary>
        public static string CraftFor(int tier)
        {
            return tier == 2 ? CraftIds.Armor2 : CraftIds.Armor3;
        }

        public static int BaseArmorTier(IReadOnlyList<string>? crafted)
        {
            return HasCrafted(crafted, CraftIds.Armor3) ? 3 : HasCrafted(crafted, CraftIds.Armor2) ? 2 : 1;
        }

        /// <summary>The Armería's weapon a soldier carries in the hand (docs/FASE8.md §A), if any.</summary>
        /// <remarks>`s: Pick&lt;CampaignSoldier, 'id'&gt;`: the soldier's id.</remarks>
        public static FoundWeapon? FoundWeaponOf(IReadOnlyList<FoundWeapon>? foundWeapons, string soldierId)
        {
            return foundWeapons?.FirstOrDefault((w) => w.Holder == soldierId && Content.FoundWeapons[w.Kind].Slot == FoundWeaponSlots.Primary);
        }

        /// <summary>The machete a soldier carries at the belt, if any.</summary>
        public static FoundWeapon? MeleeWeaponOf(IReadOnlyList<FoundWeapon>? foundWeapons, string soldierId)
        {
            return foundWeapons?.FirstOrDefault((w) => w.Holder == soldierId && Content.FoundWeapons[w.Kind].Slot == FoundWeaponSlots.Melee);
        }

        /// <summary>Weapons have no tiers (docs/FASE9.md §5): every weapon is what it is. The client still draws by it.</summary>
        /// <remarks>`weaponTier(_c?, _s?)`: it reads neither.</remarks>
        public static int WeaponTier()
        {
            return 1;
        }

        /// <summary>The armour the Taller has made is for everyone (docs/FASE9.md §5).</summary>
        /// <remarks>`armorTier(c, _s?)`: it reads only `c.crafted`.</remarks>
        public static int ArmorTier(IReadOnlyList<string>? crafted)
        {
            return BaseArmorTier(crafted);
        }

        public static readonly IReadOnlyList<ArmorTierDef> ArmorTiers = new[]
        {
            new ArmorTierDef { Name = "Ropa reforzada", Hp = 0, Armor = 0 },
            new ArmorTierDef { Name = "Armadura de placas", Hp = 2, Armor = 0 },
            new ArmorTierDef { Name = "Equipo antidisturbios", Hp = 4, Armor = 1 },
        };

        /// <summary>A weapon by its name (the tier is from before Fase 9 and changes nothing).</summary>
        public static string WeaponName(string id, int tier = 1)
        {
            return Content.Weapons[id].Name;
        }

        /// <summary>
        /// What stands between a soldier and the next tier, or null at the top: the
        /// Taller's work (and the Bastion's level that allows it). Only armour has
        /// tiers now (docs/FASE9.md §5).
        /// </summary>
        /// <remarks>
        /// `c` is read for its `level`, `techs` and `crafted`; the soldier (`_s`) not at all. `kind`: 'weapon' or 'armor'.
        /// </remarks>
        public static string? NextTierBlocker(int? level, IReadOnlyList<string>? techs, IReadOnlyList<string>? crafted, string kind)
        {
            // No weapon tiers (docs/FASE9.md §5); the armour, for everyone.
            if (kind == "weapon") return null;
            var tier = ArmorTier(crafted);
            if (tier >= 3) return null;
            var next = tier + 1;
            var needs = new List<string>();
            var craft = Crafts.TryGetValue(CraftFor(next), out var found) ? found : null;
            if (craft != null && !HasCrafted(crafted, craft.Id))
            {
                if (!string.IsNullOrEmpty(craft.Tech) && !HasTech(level, techs, craft.Tech!)) needs.Add(Lore.BastionLevelThe(Techs[craft.Tech!].Level));
                needs.Add("fabricarlas en el Taller");
            }
            return $"{craft?.Name ?? $"Nivel {next}"}: requiere {string.Join(" y ", needs)}";
        }

        /// <summary>Bastion level that lets a facility unlocked at `base` rise to level `to` (2 or 3).</summary>
        public static int UpgradeLevel(int @base, int to)
        {
            return Math.Min(8, @base + (to <= 2 ? 2 : 4));
        }

        /// <summary>What the base can build (docs/FASE12.md §11), by the Bastion level that opens it: every piece but the caravan; `building`, it rises levels.</summary>
        /// <remarks>A Record built with Object.fromEntries, walked by Object.values: a JsMap, in PIECES' order.</remarks>
        public static readonly JsMap<string, BaseUnlock> BaseUnlocks = new JsMap<string, BaseUnlock>(
            Base.PieceOrder
                .Select((id) => Base.Pieces[id])
                .Where((p) => p.Id != PieceIds.Caravan)
                .Select((p) => new KeyValuePair<string, BaseUnlock>(p.Id, new BaseUnlock { Name = p.Name, Level = p.Level, Building = p.Kind == PieceKinds.Building })));

        /// <summary>`Object.values(ITEMS)`'s order: content.ts's keys, which is not ItemIds.All's.</summary>
        private static readonly IReadOnlyList<string> ItemOrder = new[]
        {
            ItemIds.FragGrenade, ItemIds.SmokeGrenade, ItemIds.FirstAid, ItemIds.Flashbang, ItemIds.TracerRounds, ItemIds.NanoVest,
            ItemIds.ApRounds, ItemIds.MindShield,
        };

        /// <summary>Everything a level brings, for the progression screen and level-up alerts: by default, the new base's pieces.</summary>
        public static List<string> LevelUnlocks(int level, JsMap<string, BaseUnlock>? facilities = null)
        {
            var all = facilities ?? BaseUnlocks;
            var @out = new List<string>();
            @out.AddRange(TechIds.All.Select((id) => Techs[id]).Where((t) => t.Level == level).Select((t) => t.Name));
            // Belt accessories only: the special rounds are the weapons' mods now (docs/FASE9.md §4).
            @out.AddRange(ItemOrder.Select((id) => Content.Items[id]).Where((i) => i.Level == level && i.Slot == ItemSlots.Utility).Select((i) => i.Name));
            @out.AddRange(all.Values.Where((f) => f.Level == level).Select((f) => f.Name));
            @out.AddRange(all.Values
                .Where((f) => f.Building != false)
                .SelectMany((f) => new[] { 2, 3 }.Where((to) => UpgradeLevel(f.Level, to) == level).Select((to) => $"{f.Name}, nivel {to}")));
            return @out;
        }
    }
}
