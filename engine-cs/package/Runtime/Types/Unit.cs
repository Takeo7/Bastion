#nullable enable
// types.ts: a unit on the field, what it looks like, carries and has learnt.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>
    /// How a soldier looks; colours are `#rrggbb`. Purely cosmetic. The pieces
    /// are optional: without one, the piece wears what it wore before pieces
    /// could be painted (`partColor`), so older looks stay as they were. The
    /// owner's mark is never part of the look: it always glows in their colour.
    /// </summary>
    public sealed class Appearance
    {
        /// <summary>Main armour colour: every piece without its own.</summary>
        public string Primary { get; set; } = "";
        /// <summary>Plates, trim and cloth.</summary>
        public string Secondary { get; set; } = "";
        /// <summary>Visor and lens glow.</summary>
        public string Visor { get; set; } = "";
        public string Head { get; set; } = ""; // HeadIds
        public string Build { get; set; } = ""; // BuildIds
        /// <summary>The soldier's sex (from their name): with `character` unset, the client picks a model of that sex.</summary>
        public string? Sex { get; set; } // Sexes
        /// <summary>The headgear: a helmet's shell, or a beret, cap or hood (those follow `secondary` when unset).</summary>
        public string? Helmet { get; set; }
        /// <summary>Chest, back, belt and packs.</summary>
        public string? Torso { get; set; }
        /// <summary>Shoulder pads and bracers.</summary>
        public string? Arms { get; set; }
        /// <summary>Thigh and shin plates.</summary>
        public string? Legs { get; set; }
        /// <summary>Which survivor model the soldier is (the client's Synty list, e.g. 'hunter'); unset = picked from the soldier's id.</summary>
        public string? Character { get; set; }
    }

    /// <summary>Appearance.sex: `'f' | 'm'`.</summary>
    public static class Sexes
    {
        public const string F = "f";
        public const string M = "m";

        public static readonly IReadOnlyList<string> All = new[] { F, M };
    }

    /// <summary>
    /// One thing found searching (docs/FASE2.md, FASE4.md): exactly one is set.
    /// Each takes a slot of the backpack. The first three are lore.ts SearchFind's.
    /// </summary>
    public sealed class Loot
    {
        /// <summary>Suministros (credits in the campaign).</summary>
        public int? Supplies { get; set; }
        /// <summary>An accessory, as the armoury sells it.</summary>
        public string? Item { get; set; } // ItemIds
        /// <summary>Medicines (a counter in the campaign; phase 3 uses them).</summary>
        public int? Medicines { get; set; }
        /// <summary>Rations (docs/FASE4.md): the Bastion eats every day.</summary>
        public int? Food { get; set; }
        /// <summary>Parts, for the Taller to make weapons and armour.</summary>
        public int? Parts { get; set; }
        /// <summary>A weapon of the Armería (content.ts FOUND_WEAPONS): to the Armería, for one soldier; equipped in the mission (docs/FASE8.md §C).</summary>
        public string? Weapon { get; set; } // FoundWeaponIds
        /// <summary>That weapon is already the Armería's (FoundWeapon.id): a soldier's own, swapped for a find. It goes back, not in as new.</summary>
        public string? WeaponId { get; set; }
        /// <summary>That weapon's condition and what is mounted on it (docs/FASE9.md §5); absent, 100 and nothing.</summary>
        public double? Condition { get; set; }
        public List<string>? Mods { get; set; } // WeaponModIds
        /// <summary>A loose improvement for a weapon, to mount in the Armería (docs/FASE9.md §5).</summary>
        public string? Mod { get; set; } // WeaponModIds
        /// <summary>Magazines (docs/FASE7.md): straight to the searcher's spares, not into the backpack.</summary>
        public int? Mags { get; set; }
    }

    /// <summary>
    /// What a campaign soldier carries (docs/FASE8.md §A), besides the pistol everyone has: the class no longer
    /// brings a weapon. Absent, the template's (skirmish, the enemies, missions saved before).
    /// </summary>
    public sealed class Arms
    {
        /// <summary>In the hand; null, none: the pistol is the main gun.</summary>
        public string? Primary { get; set; } // FoundWeaponIds
        /// <summary>The Armería's weapon it is (FoundWeapon.id); absent, picked up in this mission.</summary>
        public string? PrimaryId { get; set; }
        /// <summary>That weapon's condition, 0 to 100 (docs/FASE9.md §5); absent, 100. Every shot wears it.</summary>
        public double? Condition { get; set; } // Fractional: Mantenimiento halves the wear (events.ts `shot`).
        /// <summary>What is mounted on it.</summary>
        public List<string>? Mods { get; set; } // WeaponModIds
        /// <summary>At the belt; null, the knife.</summary>
        public string? Melee { get; set; } // FoundWeaponIds
        public string? MeleeId { get; set; }
    }

    /// <summary>
    /// Bonuses baked into a unit when a mission starts (rank, perks and the
    /// squad's engineering upgrades). Aliens have all zeros.
    /// </summary>
    public sealed class UnitMods
    {
        public int Aim { get; set; }
        public int Crit { get; set; }
        public int Defense { get; set; }
        /// <summary>Extra tiles per action point.</summary>
        public int Mobility { get; set; }
        /// <summary>Primary weapon damage.</summary>
        public int Damage { get; set; }
        public int MeleeDamage { get; set; }
        public int PistolAim { get; set; }
        /// <summary>Bonus within 4 tiles.</summary>
        public int CloseRangeAim { get; set; }
        public int CloseRangeCrit { get; set; }
        public int GrenadeDamage { get; set; }
        public int GrenadeRadius { get; set; }
        public int Heal { get; set; }
        public int Aid { get; set; }
        public int MeleeAim { get; set; }
        /// <summary>Extra damage against flanked targets.</summary>
        public int FlankDamage { get; set; }
        /// <summary>Extra armour shredded by primary weapon hits.</summary>
        public int Shred { get; set; }
        public int Will { get; set; }
        /// <summary>Armour ignored by primary weapon hits (armour-piercing rounds).</summary>
        public int Pierce { get; set; }
        /// <summary>Graze chance against this unit (talents add to the template's).</summary>
        public int Dodge { get; set; }
        /// <summary>Extra damage on critical hits.</summary>
        public int CritDamage { get; set; }
        public int MeleeCrit { get; set; }
        public int PistolDamage { get; set; }
        public int PistolCrit { get; set; }
        /// <summary>Extra damage within 4 tiles.</summary>
        public int CloseDamage { get; set; }
        /// <summary>Aim when shooting down from a higher level.</summary>
        public int HeightAim { get; set; }
        /// <summary>Aim lost to the reaction-shot penalty is cut by this many percentage points.</summary>
        public int ReactionKeep { get; set; }
        /// <summary>Penalty taken off reaction shots aimed at this unit.</summary>
        public int ReactionDefense { get; set; }
        /// <summary>Defence while on overwatch.</summary>
        public int WatchDefense { get; set; }
        /// <summary>Squadsight costs this much less aim per tile.</summary>
        public int SquadsightKeep { get; set; }
        /// <summary>"Llave doble": aim with a partner's soldier within 4 tiles of the shooter.</summary>
        public int PairAim { get; set; }
        /// <summary>"Llave doble": defence with a partner's soldier within 4 tiles.</summary>
        public int PairDefense { get; set; }
        /// <summary>"Llave doble": aim against targets within 4 tiles of a partner's soldier.</summary>
        public int SpotterAim { get; set; }
        public int GrenadeShred { get; set; }
        public int GrenadeRange { get; set; }
        /// <summary>Extra aim allies get from this unit's holo-targeting.</summary>
        public int HoloAim { get; set; }
        /// <summary>Extra aim this unit's Suppression takes away.</summary>
        public int SuppressAim { get; set; }
        public int Hack { get; set; }
        /// <summary>Against the Chillón's scream: 1 = never panics («Nervios de acero»), 2 = immune («Serenidad»).</summary>
        public int PsiGuard { get; set; }
        /// <summary>Golpe al oído (board.ts): the Slash disorients on a hit.</summary>
        public int SlashDisorient { get; set; }
        /// <summary>Ojos del viento: extra tiles of sight.</summary>
        public int Sight { get; set; }
        /// <summary>Ojos del viento: smoke doesn't shield targets from this unit (1/0).</summary>
        public int SmokeSight { get; set; }
        /// <summary>Voz tranquila and the like (board.ts): +10 will per level to allies within 4 tiles.</summary>
        public int CalmAura { get; set; }
        // Fase 9 (skills.ts).
        /// <summary>Tiro: aim with firearms (the pistol too), not with the blade.</summary>
        public int GunAim { get; set; }
        /// <summary>Tiro 3: crit with firearms.</summary>
        public int GunCrit { get; set; }
        /// <summary>Sin hacer ruido (1/0): searching, opening boxes and forcing make no noise.</summary>
        public int Quiet { get; set; }
        // Fase 10 (board.ts).
        /// <summary>The stone's extra damage, and how many tiles farther it is heard.</summary>
        public int StoneDamage { get; set; }
        public int StoneNoise { get; set; }
        /// <summary>Mantenimiento (1/0): its weapons wear half.</summary>
        public int Care { get; set; }
    }

    public sealed class Unit
    {
        public string Id { get; set; } = "";
        public string Team { get; set; } = ""; // Teams
        public string Template { get; set; } = ""; // TemplateIds
        public string Name { get; set; } = "";
        /// <summary>Owning player for XCOM soldiers; null for aliens and shared (pool) units.</summary>
        public int? Owner { get; set; } // Slot
        public Pos Pos { get; set; } = new Pos();
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        /// <summary>Damage absorbed per hit; explosives shred it.</summary>
        public int Armor { get; set; }
        public int Ap { get; set; }
        public int Ammo { get; set; }
        /// <summary>Spare magazines for the primary weapon (docs/FASE7.md): only the squad counts them; without it, reloading never runs out.</summary>
        public int? Mags { get; set; }
        /// <summary>A campaign soldier's own weapons (docs/FASE8.md §A); absent, the template's.</summary>
        public Arms? Arms { get; set; }
        /// <summary>Rounds in the pistol carried beside a primary weapon (content.ts PISTOL_CLIP); `ammo` is the main gun's.</summary>
        public int? PistolAmmo { get; set; }
        public Dictionary<string, int> Charges { get; set; } = new Dictionary<string, int>(); // ChargeIds → uses (Partial<Record>)
        public bool Overwatch { get; set; }
        public bool Hunkered { get; set; }
        /// <summary>Aid Protocol: defense bonus until the start of the squad's next turn (0 = none).</summary>
        public int Aided { get; set; }
        /// <summary>Stunned units start their next turn with a single action.</summary>
        public bool Stunned { get; set; }
        /// <summary>False once dead or evacuated: the unit is no longer on the battlefield.</summary>
        public bool Alive { get; set; }
        public bool Evacuated { get; set; }
        public string? PodId { get; set; }
        /// <summary>Left behind when the bus went (docs/PRUEBA-2026-10-14.md P1): off the field, and lost to the campaign.</summary>
        public bool? Lost { get; set; }
        public int Rank { get; set; }
        public List<string> Perks { get; set; } = new List<string>(); // PerkIds
        public UnitMods Mods { get; set; } = new UnitMods();
        public int Kills { get; set; }
        /// <summary>Soldier id in the campaign, when the mission belongs to one.</summary>
        public string? CampaignId { get; set; }
        /// <summary>Campaign soldiers: chosen look and nickname (absent for aliens and skirmish).</summary>
        public Appearance? Appearance { get; set; }
        public string? Nickname { get; set; }
        /// <summary>Campaign soldiers: weapon and armour tier they carry (1 when absent).</summary>
        public int? WeaponTier { get; set; }
        public int? ArmorTier { get; set; }
        /// <summary>Campaign soldiers carrying a found weapon (docs/FASE4.md): its name, for the card; absent, the class weapon of its tier.</summary>
        public string? WeaponName { get; set; }
        /// <summary>Mission objective (the relay): never acts, doesn't count as a hostile.</summary>
        public bool Objective { get; set; }
        /// <summary>VIP waiting to be reached: can't act and the aliens ignore them.</summary>
        public bool Captive { get; set; }
        /// <summary>Resistance to psionic attacks.</summary>
        public int Will { get; set; }
        /// <summary>Turns left before each ability can be used again.</summary>
        public Dictionary<string, int> Cooldowns { get; set; } = new Dictionary<string, int>(); // AbilityIds → turns (Partial<Record>)
        /// <summary>Kill Zone: overwatch that keeps firing, once per enemy (ids already shot).</summary>
        public bool KillZone { get; set; }
        public List<string> ReactedTo { get; set; } = new List<string>();
        /// <summary>Overwatch with the blade (docs/FASE10.md §4): strikes whoever comes alongside instead of shooting.</summary>
        public bool? WatchMelee { get; set; }
        /// <summary>A soldier's experience lines in the mission (skills.ts MissionLines): its bars fill out there.</summary>
        public MissionLines? Lines { get; set; }
        /// <summary>A vest found and put on out there (playtest 16/10 #18): its health counts at once, and it comes home.</summary>
        public List<string>? Worn { get; set; } // ItemIds
        /// <summary>
        /// A defence of the base that acts on its own (a turret, docs/FASE12.md §11.4):
        /// nobody gives it orders, it has no turn, it keeps watch in the enemy's and
        /// it doesn't hold the base (only soldiers do). `piece`: its piece on the base.
        /// </summary>
        public Automaton? Automaton { get; set; }
        /// <summary>Belt things found and put on the belt out there (playtest 18/10 #5): usable at once; home if not dipped into (loot.ts beltedBack).</summary>
        public List<string>? Belted { get; set; } // ItemIds
        /// <summary>
        /// Where the unit looks, in radians round the map (0 along +x, π/2 along +y):
        /// the way it last walked, struck or heard something. The dead see only in
        /// a cone that way (los.ts inViewCone, Arturo 17/10).
        /// </summary>
        public double? Facing { get; set; }
        /// <summary>Unit this one keeps pinned down with Suppression.</summary>
        public string? Suppressing { get; set; }
        /// <summary>−aim while pinned down; moving draws fire.</summary>
        public string? SuppressedBy { get; set; }
        /// <summary>Holo-targeting: aim bonus allies get against this unit.</summary>
        public int Marked { get; set; }
        /// <summary>Rupture: extra damage taken from every hit.</summary>
        public int Ruptured { get; set; }
        /// <summary>Turns of disorientation left (−aim, no explosives).</summary>
        public int Disoriented { get; set; }
        /// <summary>Loses its next turn cowering.</summary>
        public bool Panicked { get; set; }
        /// <summary>Alien that mind-controls this soldier, and the turns it lasts.</summary>
        public string? ControlledBy { get; set; }
        public int ControlTurns { get; set; }
        /// <summary>A corpse raised by a sectoid can't be raised again.</summary>
        public bool Reanimated { get; set; }
        /// <summary>Implacable / Death From Above refund an action once per turn.</summary>
        public bool Refunded { get; set; }
        /// <summary>Equipment carried (grants abilities such as smoke grenades).</summary>
        public List<string> Gear { get; set; } = new List<string>(); // ItemIds
        /// <summary>Enemies: where the last noise it heard came from (docs/FASE2.md); it goes to look while it sees nobody.</summary>
        public Pos? Heard { get; set; } // `heard?: Pos | null`: absent and null both mean none.
        /// <summary>
        /// The dead: the soldier it last saw, whom it follows wherever they go while it sees none of the living, and how
        /// many of its turns have gone by since; after HUNT_TURNS of them it forgets them (Arturo, 22/10). Each its own:
        /// the dead don't tell each other.
        /// </summary>
        public Hunting? Hunting { get; set; } // `hunting?: {…} | null`: absent and null both mean none.
        /// <summary>Soldiers: what they found searching (BACKPACK_SLOTS at most). It counts if they get out or stand at a victory.</summary>
        public List<Loot>? Backpack { get; set; }
        /// <summary>Backpack slots when not BACKPACK_SLOTS (the Chapistería's one more, docs/FASE8.md §C).</summary>
        public int? PackSlots { get; set; }
        /// <summary>Soldiers: what they did this mission that teaches (Fase 9, skills.ts), counted by the events.</summary>
        public Deeds? Deeds { get; set; }
        /// <summary>A zombie risen from a soldier of the squad (its id): it keeps that soldier's `appearance` (playtest 2026-10-16).</summary>
        public string? RisenFrom { get; set; }
        /// <summary>Bitten and infected (docs/FASE3.md): no effect in combat, but if it falls it rises, and back at base it turns.</summary>
        public bool? Infected { get; set; }
    }

    /// <summary>Unit.automaton: `{ piece: string }`.</summary>
    public sealed class Automaton
    {
        public string Piece { get; set; } = "";
    }

    /// <summary>Unit.hunting: `{ id: string; missed: number }`.</summary>
    public sealed class Hunting
    {
        public string Id { get; set; } = "";
        public int Missed { get; set; }
    }
}
