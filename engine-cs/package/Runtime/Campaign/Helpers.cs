#nullable enable
// campaign.ts: the facilities' costs, the state's small readings and its helpers (what a facility does, beds, prices,
// who is fit to go out). Those that change the state (pay, log, newId, addSoldier) come with the rules that use them.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // --------------------------------------------------------------- facilities

        /// <summary>Price, days and Bastion level of raising a facility to `to` (2 or 3).</summary>
        public static FacilityUpgrade UpgradeCost(string id, int to)
        {
            var def = Facilities[id];
            return to <= 2
                ? new FacilityUpgrade { Cost = def.Cost, Days = def.Days, Level = Progression.UpgradeLevel(def.Level, 2) }
                : new FacilityUpgrade { Cost = (int)Js.Round(def.Cost * 1.5), Days = def.Days + 2, Level = Progression.UpgradeLevel(def.Level, 3) };
        }

        // -------------------------------------------------------------------- state

        /// <summary>Soldiers each player may keep: a commander alone runs both kits.</summary>
        public static int SoldierCap(CampaignState c)
        {
            return c.Players.Count > 1 ? MaxSoldiersPerPlayer : 2 * MaxSoldiersPerPlayer;
        }

        /// <summary>`days` of before as days of now (one a mission): at least one.</summary>
        public static int OldDaysToNow(int days)
        {
            return Math.Max(1, (int)Math.Ceiling(days / (double)OldDaysPerMission));
        }

        /// <summary>A saved campaign the current progression can't take (campaignOutdated): the host keeps it aside.</summary>
        /// <remarks>`c: { version: number }`: its version.</remarks>
        public static bool CampaignOutdated(int version)
        {
            return version < FreshCampaignVersion;
        }

        // ------------------------------------------------------------------ helpers

        public static bool CanAfford(CampaignState c, int cost)
        {
            return c.Credits >= cost;
        }

        public static string CostText(int cost)
        {
            return cost != 0 ? $"{cost} §" : "Gratis";
        }

        /// <summary>
        /// The sex of someone who joins by name: the story's people (lore.ts NAMED_SEX),
        /// a relative by first name (quests.ts RELATIVES), or else a soldier's name.
        /// </summary>
        public static string? SexOfPerson(string name)
        {
            if (Lore.NamedSex.TryGetValue(name, out var named)) return named;
            var first = name.Split(' ')[0];
            return Quests.Relatives.FirstOrDefault((r) => r.Name == first)?.Sex ?? Content.SexOfName(name);
        }

        /// <summary>
        /// The buildings of a facility standing and working on the base (docs/FASE12.md
        /// §11.3): one beaten down to half or less stops working until it is mended.
        /// The Sala de radio takes the Centro de comunicaciones from its level 2 and the
        /// Estación de escucha at its level 3.
        /// </summary>
        private static List<(BasePiece Piece, int Level)> FacilityPieces(CampaignState c, string id)
        {
            var found = new List<(BasePiece Piece, int Level)>();
            foreach (var p in c.Bastion?.Pieces ?? new List<BasePiece>())
            {
                if (!BuildingWorks(p)) continue;
                var level = p.Level ?? 1;
                if (id == FacilityIds.Relay || id == FacilityIds.Listening)
                {
                    if (p.Piece != PieceIds.Radio) continue;
                    var at = id == FacilityIds.Relay ? level - 1 : level - 2;
                    if (at > 0) found.Add((p, at));
                }
                else if (Base.Pieces[p.Piece].Facility == id) found.Add((p, level));
            }
            return found;
        }

        /// <summary>Whether a building on the base works: standing, with more than half of what it holds.</summary>
        public static bool BuildingWorks(BasePiece p)
        {
            var def = Base.Pieces[p.Piece];
            return def.Kind == PieceKinds.Building && p.Hp * 2 > def.Hp;
        }

        public static int FacilityCount(CampaignState c, string id)
        {
            return FacilityPieces(c, id).Count;
        }

        /// <summary>Level of a facility: its best copy's (0 when not built, or not working).</summary>
        public static int FacilityLevel(CampaignState c, string id)
        {
            var level = 0;
            foreach (var f in FacilityPieces(c, id)) level = Math.Max(level, f.Level);
            return level;
        }

        /// <summary>The base's Sala de radio, working: it hears the sieges coming (docs/FASE12.md §11.5).</summary>
        public static bool HasRadio(CampaignState c)
        {
            return (c.Bastion?.Pieces ?? new List<BasePiece>()).Any((p) => p.Piece == PieceIds.Radio && BuildingWorks(p));
        }

        /// <summary>
        /// The workshop takes 25/35/45 % off accessories. No bonus for standing next to
        /// anything any more (Ana, 21/10 #41: «quita lo de junto a… y si hay
        /// funcionalidad relacionada también»).
        /// </summary>
        public static int ShopPrice(CampaignState c, string id)
        {
            var cost = Content.Items[id].Cost;
            var level = FacilityLevel(c, FacilityIds.Workshop);
            if (level == 0) return cost;
            var off = 0.25 + 0.1 * (level - 1);
            return (int)Js.Round(cost * (1 - off));
        }

        /// <summary>What a recruit costs.</summary>
        public static int RecruitCost(CampaignState c)
        {
            return RecruitPrice;
        }

        /// <summary>Beds in the Bastion: the caravan's, and each barracón's (2/3/4).</summary>
        public static int Beds(CampaignState c)
        {
            return BunkerBeds + FacilityPieces(c, FacilityIds.Quarters).Sum((f) => 1 + f.Level);
        }

        /// <summary>Beds nobody sleeps in: recruiting needs one.</summary>
        public static int BedsFree(CampaignState c)
        {
            return Beds(c) - c.Soldiers.Count((s) => s.Alive);
        }

        /// <summary>Wound days healed each day: 1, and 2/3/4 with the infirmary.</summary>
        public static int HealRate(CampaignState c)
        {
            var level = FacilityLevel(c, FacilityIds.Infirmary);
            return level != 0 ? 1 + level : 1;
        }

        /// <summary>
        /// The suministros an operation pays, with the Sala de mapas (Arturo, 18/10
        /// #40: the Bastion has no experience now, so the maps find better pickings):
        /// +25/40/55 %.
        /// </summary>
        public static double RewardMultiplier(CampaignState c)
        {
            var level = FacilityLevel(c, FacilityIds.Simulation);
            return level != 0 ? 1.25 + 0.15 * (level - 1) : 1;
        }

        private static readonly IReadOnlyList<int> MemorialWills = new[] { 0, 0, 5, 10 };

        /// <summary>Will the Camposanto gives every soldier: +5 at level 2, +10 at level 3.</summary>
        public static int MemorialWill(CampaignState c)
        {
            return MemorialWills[FacilityLevel(c, FacilityIds.Memorial)];
        }

        private static readonly IReadOnlyList<int> RangeAims = new[] { 3, 6, 10 };

        /// <summary>Aim every soldier gets from the shooting range: 3/6/10.</summary>
        public static int RangeAim(CampaignState c)
        {
            var level = FacilityLevel(c, FacilityIds.Range);
            return level != 0 ? RangeAims[level - 1] : 0;
        }

        /// <summary>
        /// Days (missions) La Lata's workshop (the Chapistería) takes off each wound (its stretcher): one a level, one left at least.
        /// Missions take no days any more (docs/PRUEBA-2026-10-14.md P7), so that is
        /// what it does from level 1; it also gives a backpack slot (docs/FASE8.md).
        /// </summary>
        public static GarageBonus GarageCut(CampaignState c)
        {
            return new GarageBonus { Wounds = FacilityLevel(c, FacilityIds.Garage) };
        }

        /// <summary>Extra days between the enemy clock's steps: 2 of before per level of the listening post.</summary>
        public static int ListeningDelay(CampaignState c)
        {
            var level = FacilityLevel(c, FacilityIds.Listening);
            return level != 0 ? OldDaysToNow(2 * level) : 0;
        }

        private static readonly IReadOnlyList<double> CrewCuts = new[] { 0.25, 0.4, 0.5 };

        /// <summary>Days a work takes with the works brigade (−25/40/50 %), at least one.</summary>
        public static int WorkDays(CampaignState c, int days)
        {
            var level = FacilityLevel(c, FacilityIds.Crew);
            return level != 0 ? Math.Max(1, (int)Js.Round(days * (1 - CrewCuts[level - 1]))) : days;
        }

        /// <summary>Works that can go on at once: two crews, three with the works brigade.</summary>
        public static int WorkCrews(CampaignState c)
        {
            return 2 + (FacilityLevel(c, FacilityIds.Crew) != 0 ? 1 : 0);
        }

        /// <summary>Copies not equipped by any living soldier.</summary>
        public static int ItemsFree(CampaignState c, string id)
        {
            var used = c.Soldiers.Where((s) => s.Alive).Sum((s) => (s.Loadout.Utility == id ? 1 : 0) + (s.Loadout.Ammo == id ? 1 : 0));
            return (c.Inventory.TryGetValue(id, out var owned) ? owned : 0) - used;
        }

        /// <summary>Fit to deploy: alive, healed, and not running a fever from a bite.</summary>
        public static bool SoldierReady(CampaignSoldier s)
        {
            return s.Alive && s.WoundedDays == 0 && s.Infection == null;
        }

        /// <summary>
        /// Nobody fit to go out and nobody to recruit (18/10 test: after a fall, only
        /// the wounded left and no suministros): time, which only goes by with the
        /// missions, would stand still. Then, and only then, one waits for them to heal.
        /// </summary>
        public static bool MustWait(CampaignState c)
        {
            return c.Outcome == null && c.ActiveMission == null && !c.Soldiers.Any(SoldierReady) && !(CanAfford(c, RecruitCost(c)) && BedsFree(c) > 0);
        }
    }
}
