#nullable enable
// campaign.ts: infection, food, the Taller and the Armería (docs/FASE3.md, FASE4.md, FASE9.md §5), what can be read of
// them. turn, addFood and wornAs, which change the state, come with the rules that use them.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ------------------------------------------------------------ infection

        /// <summary>Medicines a treatment takes: 2, or 1 with the Enfermería at level 2 or more.</summary>
        public static int TreatCost(CampaignState c)
        {
            return FacilityLevel(c, FacilityIds.Infirmary) >= 2 ? 1 : 2;
        }

        /// <summary>The day a bitten soldier turns, unless treated: the day after coming back, two with an Enfermería, and one more for each level of the Cuarentena past the first.</summary>
        public static int? TurnDay(CampaignState c, CampaignSoldier s)
        {
            if (s.Infection == null) return null;
            return s.Infection.Since + TurnDays + (FacilityCount(c, FacilityIds.Infirmary) != 0 ? InfirmaryTurnDays : 0) + Math.Max(0, FacilityLevel(c, FacilityIds.Quarantine) - 1);
        }

        /// <summary>
        /// Whether the bitten may board La Lata (playtest 2026-10-15, #13): only with a Cuarentena to take them to, once the
        /// Bastion is big enough to build one (Ana, 21/10 #36: not at the caravan's level); before that, they board.
        /// </summary>
        public static bool HasQuarantine(CampaignState c)
        {
            return FacilityCount(c, FacilityIds.Quarantine) > 0 || Progression.BastionLevel(c.Level) < Base.Pieces[PieceIds.Quarantine].Level;
        }

        /// <summary>An enclave that is an ally (contacted) and stands (no Nido in its region).</summary>
        private static bool EnclaveStands(CampaignState c, string id)
        {
            var r = c.Regions.FirstOrDefault((x) => x.Id == id);
            return r != null && r.Contacted && r.Facility == null && !UnderMarea(c, id);
        }

        /// <summary>Enclaves that send medicines (ENCLAVES).</summary>
        private static List<string> LivingEnclaves(CampaignState c)
        {
            return Enclaves.Where((id) => EnclaveStands(c, id)).ToList();
        }

        // ------------------------------------------------------------------- food

        /// <summary>Rations the Bastion eats a day.</summary>
        public static int DailyRations(CampaignState c)
        {
            return (int)Math.Ceiling(c.Soldiers.Count((s) => s.Alive) / (double)EatersPerRation);
        }

        /// <summary>Rations the Huerto grows a day: five a level.</summary>
        public static int GardenYield(CampaignState c)
        {
            // Every Huerto that works gives its own (Arturo, 20/10: several of them, each its rations), by its level.
            return FacilityPieces(c, FacilityIds.Garden).Sum((f) => GardenRations * f.Level);
        }

        /// <summary>Monthly payment from the Council before region income.</summary>
        private const int BaseIncome = 60;

        /// <summary>What Bielsa's next convoy brings (UI's «Bastión» panel): the base, and each zone with a deal not under the Marea.</summary>
        public static int ConvoyIncome(CampaignState c)
        {
            return BaseIncome + c.Regions.Where((r) => r.Contacted && !UnderMarea(c, r.Id)).Sum((r) => RegionIncome(c, r.Id));
        }

        /// <summary>Days (missions) until the next convoy.</summary>
        public static int ConvoyIn(CampaignState c)
        {
            return Math.Max(0, c.NextSupplyDay - c.Day);
        }

        /// <summary>Rations eaten a mission (UI's «Bastión» panel): a day's.</summary>
        public static int RationsPerMission(CampaignState c)
        {
            return DailyRations(c) * DaysPerMission;
        }

        /// <summary>Rations the Huertos grow a mission.</summary>
        public static int GardenPerMission(CampaignState c)
        {
            return GardenYield(c) * DaysPerMission;
        }

        // ---------------------------------------------------------- the Taller

        /// <summary>The Taller on the base, working (docs/FASE12.md §11.3).</summary>
        private static BasePiece? WorkshopPiece(CampaignState c)
        {
            var pieces = FacilityPieces(c, FacilityIds.Workshop);
            return pieces.Count > 0 ? pieces[0].Piece : null;
        }

        /// <summary>Why the Taller can't start making `id` now, or null (docs/FASE4.md).</summary>
        public static string? CraftBlocker(CampaignState c, string id)
        {
            var def = Progression.Crafts[id];
            // A weapon or an improvement for the Armería is made again and again (docs/FASE8.md §A).
            if (def.Kind != CraftKinds.Arms && def.Kind != CraftKinds.Mod && Progression.HasCrafted(c.Crafted, id)) return "Ya está hecho.";
            if (c.Base.Works.Any((w) => w.Project == id)) return "Ya se está haciendo.";
            var shop = WorkshopPiece(c);
            if (shop == null) return "Hace falta el Taller.";
            if ((shop.Level ?? 1) < def.Workshop) return $"Hace falta el Taller a nivel {def.Workshop}.";
            if (!string.IsNullOrEmpty(def.Tech) && !Progression.HasTech(c.Level, c.Techs, def.Tech!)) return Lore.NeedsLevelLine(Progression.Techs[def.Tech!].Level);
            if ((def.Level ?? 0) != 0 && Progression.BastionLevel(c.Level) < def.Level!.Value) return Lore.NeedsLevelLine(def.Level.Value);
            if (!string.IsNullOrEmpty(def.After) && !Progression.HasCrafted(c.Crafted, def.After!)) return $"Antes hay que hacer {Progression.Crafts[def.After!].The}.";
            if (c.Base.Works.Any((w) => w.Kind == ConstructionKinds.Craft)) return "El Taller ya está con otro encargo.";
            if (!CanAfford(c, def.Cost)) return "No hay suministros suficientes.";
            if ((c.Parts ?? 0) < def.Parts) return "No hay piezas suficientes.";
            return null;
        }

        /// <summary>What a soldier's card reads for the weapon in hand: a good one by its name, a plain one by its tier's; without, the pistol.</summary>
        public static string WeaponLabel(CampaignState c, CampaignSoldier s)
        {
            var found = Progression.FoundWeaponOf(c.FoundWeapons, s.Id);
            if (found == null) return Content.Weapons[WeaponIds.Pistol].Name;
            return Content.FoundWeapons[found.Kind].Name;
        }

        /// <summary>What a soldier carries from the Armería (docs/FASE8.md §A): the weapon in hand, if any, and the machete or the knife.</summary>
        public static Arms SoldierArms(CampaignState c, CampaignSoldier s)
        {
            var primary = Progression.FoundWeaponOf(c.FoundWeapons, s.Id);
            var melee = Progression.MeleeWeaponOf(c.FoundWeapons, s.Id);
            return new Arms
            {
                Primary = primary?.Kind,
                PrimaryId = primary?.Id,
                Condition = primary?.Condition,
                Mods = primary?.Mods != null && primary.Mods.Count > 0 ? new List<string>(primary.Mods) : null,
                Melee = melee?.Kind,
                MeleeId = melee?.Id,
            };
        }

        // --------------------------------------------- the Armería (docs/FASE9.md §5)

        /// <summary>A weapon of the Armería a commander may work on: on the rack, or carried by one of theirs.</summary>
        private static string? WeaponBlocker(CampaignState c, int slot, string id, IReadOnlyList<int>? connected)
        {
            var w = (c.FoundWeapons ?? new List<FoundWeapon>()).FirstOrDefault((x) => x.Id == id);
            if (w == null) return "Esa arma no está en la Armería.";
            var holder = !string.IsNullOrEmpty(w.Holder) ? c.Soldiers.FirstOrDefault((s) => s.Id == w.Holder) : null;
            if (holder != null && !Content.CommandsFor(slot, holder.Owner, connected)) return $"La lleva {holder.Name}, de la escuadra del otro jugador.";
            return null;
        }

        /// <summary>Parts it takes to leave a weapon as new: one every REPAIR_PER_PART of condition missing.</summary>
        /// <remarks>`w: Pick&lt;FoundWeapon, 'condition'&gt;`.</remarks>
        public static int RepairParts(FoundWeapon w)
        {
            return (int)Math.Ceiling((100 - (w.Condition ?? 100)) / Content.RepairPerPart);
        }

        /// <summary>Why a weapon can't be mended now, or null: the parts there are mend what they can.</summary>
        public static string? RepairBlocker(CampaignState c, int slot, string id, IReadOnlyList<int>? connected = null)
        {
            var blocked = WeaponBlocker(c, slot, id, connected);
            if (blocked != null) return blocked;
            var w = c.FoundWeapons!.First((x) => x.Id == id);
            if (Content.FoundWeapons[w.Kind].Slot != FoundWeaponSlots.Primary || RepairParts(w) == 0) return "Está como nueva.";
            if ((c.Parts ?? 0) == 0) return "No hay piezas.";
            return null;
        }

        /// <summary>Why `mod` can't go on (`on`) or come off that weapon, or null.</summary>
        public static string? ModBlocker(CampaignState c, int slot, string id, string mod, bool on, IReadOnlyList<int>? connected = null)
        {
            var blocked = WeaponBlocker(c, slot, id, connected);
            if (blocked != null) return blocked;
            var w = c.FoundWeapons!.First((x) => x.Id == id);
            var mounted = w.Mods ?? new List<string>();
            if (!on) return mounted.Contains(mod) ? null : "No la lleva puesta.";
            if (Content.FoundWeapons[w.Kind].Slot != FoundWeaponSlots.Primary) return "Solo las armas de fuego llevan mejoras.";
            if (mounted.Contains(mod)) return "Ya la lleva puesta.";
            if (mounted.Count >= Content.ModSlots) return $"Lleva ya {Content.ModSlots} mejoras: quita una antes.";
            if ((c.WeaponMods != null && c.WeaponMods.TryGetValue(mod, out var loose) ? loose : 0) == 0) return $"No queda {Content.WeaponMods[mod].None} en la Armería.";
            return null;
        }
    }
}
