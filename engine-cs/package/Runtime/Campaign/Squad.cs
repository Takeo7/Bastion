#nullable enable
// campaign.ts: La Lata and the squad (who goes, on which pad), what a shared decision costs, and the deployment (what
// each soldier takes to a mission) and the Bastion's level. joinSquad, syncPads and the commands come with the rules.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ----------------------------------------------------------------- the squad

        /// <summary>La Lata's vehicle (campaigns before version 22 had the bus's six seats: the furgoneta).</summary>
        public static int VehicleOf(CampaignState c)
        {
            return c.Vehicle ?? 1;
        }

        /// <summary>Max soldiers each player may bring: half of La Lata (rounded up), all of it alone.</summary>
        public static int SquadLimit(CampaignState c, IReadOnlyList<int> connected)
        {
            var seats = SquadMax(c);
            return c.Players.Count > 1 && connected.Count > 1 ? (seats + 1) / 2 : seats;
        }

        public static int SquadMax(CampaignState c)
        {
            return VehicleSeats[VehicleOf(c)];
        }

        private static int SquadCount(CampaignState c, int slot)
        {
            return c.Squad.Count((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)?.Owner == slot);
        }

        /// <summary>
        /// Whether `slot` can put one more soldier in the squad (UX-REVISION-2 #39).
        /// Each player has a share; a share the partner can't fill (no ready soldier
        /// left out) can be taken by the other. A place lent that way comes back when
        /// its owner wants it: `evict` is the soldier that leaves to make room.
        /// </summary>
        public static SquadPlaceResult SquadPlace(CampaignState c, int slot, IReadOnlyList<int> connected)
        {
            // Each their own part (Arturo, 18/10 #43): a part the other leaves empty stays empty.
            if (c.Squad.Count >= SquadMax(c)) return new SquadPlaceResult { Error = $"La Lata va llena: caben {SquadMax(c)}." };
            var limit = SquadLimit(c, connected);
            return SquadCount(c, slot) < limit ? new SquadPlaceResult() : new SquadPlaceResult { Error = $"Máximo {limit} soldados por jugador." };
        }

        /// <summary>The more veterancy (and then missions) first.</summary>
        private static int SeasonedFirst(CampaignSoldier a, CampaignSoldier b)
        {
            var d = b.Board.Count - a.Board.Count;
            if (d != 0) return d;
            d = Bars.TotalBars(b.Skills) - Bars.TotalBars(a.Skills);
            return d != 0 ? d : b.Missions - a.Missions;
        }

        /// <summary>
        /// The squad a mission starts with: each connected player's ready soldiers,
        /// the most seasoned first (docs/FASE9.md: veterancy, then missions flown).
        /// </summary>
        public static List<string> DefaultSquad(CampaignState c, IReadOnlyList<int> connected)
        {
            var limit = SquadLimit(c, connected);
            var squad = new List<string>();
            foreach (var slot in c.Players.Where((p) => connected.Contains(p)))
            {
                var ready = c.Soldiers.Where((s) => s.Owner == slot && SoldierReady(s)).ToList();
                Js.Sort(ready, SeasonedFirst);
                squad.AddRange(ready.Take(limit).Select((s) => s.Id));
            }
            // A part left short stays short: each their own (Arturo, 18/10 #43).
            return squad.Take(SquadMax(c)).ToList();
        }

        /// <summary>
        /// The hangar pads of the squad, `squadMax` long. Each commander has a half of
        /// their own (the first player the left one) and keeps each soldier on the pad
        /// it took; a place lent by the partner is taken from the middle of their half.
        /// Pure: both commanders work out the same pads from the same campaign.
        /// </summary>
        public static List<string?> SquadPads(CampaignState c)
        {
            var max = SquadMax(c);
            var pads = Enumerable.Repeat((string?)null, max).ToList();
            var kept = c.Pads ?? new Dictionary<string, int>();
            var half = (max + 1) / 2;
            // A kept pad on the other one's half is let go: a rookie handed over by addPlayer, or a squad picked alone across
            // both halves, stood among the partner's soldiers ever after (Ana, 23/10 #19).
            bool Own(string id, int pad) => c.Players.Count < 2 || (c.Soldiers.FirstOrDefault((s) => s.Id == id)?.Owner == c.Players[0]) == (pad < half);
            foreach (var id in c.Squad)
            {
                if (kept.TryGetValue(id, out var pad) && pad < max && pad >= 0 && pads[pad] == null && Own(id, pad)) pads[pad] = id;
            }
            var all = Enumerable.Range(0, max).ToList();
            var left = all.Take(half).ToList();
            var right = all.Skip(half).ToList();
            foreach (var id in c.Squad)
            {
                if (pads.Contains(id)) continue;
                var owner = c.Soldiers.FirstOrDefault((s) => s.Id == id)?.Owner;
                var order = c.Players.Count < 2 ? all : owner == c.Players[0] ? left.Concat(right).ToList() : right.Concat(Enumerable.Reverse(left)).ToList();
                foreach (var i in order)
                {
                    if (pads[i] != null) continue;
                    pads[i] = id;
                    break;
                }
            }
            return pads;
        }

        private static bool EveryoneReady(bool[] ready, IReadOnlyList<int> connected)
        {
            return connected.Count > 0 && connected.All((s) => ready[s]);
        }

        // ---------------------------------------------------------------- proposals

        /// <summary>
        /// The suministros a shared decision costs, paid only when the partner accepts
        /// (applyAction): what the counter shows as committed meanwhile (playtest 2026-10-16).
        /// </summary>
        public static int ProposalCost(CampaignState c, ProposalAction action)
        {
            switch (action)
            {
                case PlaceAction a:
                    return Base.Pieces[a.Piece].Cost.Supplies;
                case RaiseAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault((x) => x.Id == a.Piece);
                    var next = p != null ? RaiseCost(p) : null;
                    return next != null && next.Error == null ? next.Cost : 0;
                }
                case PlaceLineAction a:
                    return Base.Pieces[a.Piece].Cost.Supplies * a.Cells.Count;
                case FellAction a:
                    return Base.FellCost * a.Tiles.Count;
                case MendAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault((x) => x.Id == a.Piece);
                    return p != null ? Base.RepairCost(p).Supplies : 0;
                }
                case RearmAction _:
                case DismantleAction _:
                    return 0;
                case LevelUpAction _:
                    return Js.At(Progression.LevelCost, Progression.BastionLevel(c.Level), 0);
                case ContactAction _:
                    return ContactCost(c);
                case CraftAction a:
                    return Progression.Crafts[a.Project].Cost;
                case RecruitAction _:
                    return RecruitCost(c);
                case BuyAction a:
                    return ShopPrice(c, a.Item);
                default:
                    return 0;
            }
        }

        // ------------------------------------------------------------- deployment

        /// <summary>Days of before off a wound costs: these, and as many again per point of health lost (oldDaysToNow).</summary>
        private const int WoundDays = 2;

        /// <summary>
        /// Everything a soldier brings to a mission (docs/FASE9.md): its skills,
        /// talents and veterancy, its belt, its weapons and the armour the Taller made.
        /// </summary>
        public static SquadMember SquadMember(CampaignState c, CampaignSoldier s)
        {
            var gear = new[] { s.Loadout.Utility, s.Loadout.Ammo }.Where((g) => !string.IsNullOrEmpty(g)).Select((g) => g!).ToList();
            var arms = SoldierArms(c, s);
            var stats = Bars.FighterStatsOf(s.Skills, s.Growth, s.Board, gear, arms);
            var mods = stats.Mods;
            var maxHp = stats.MaxHp;
            var armor = stats.Armor;
            var charges = stats.Charges;
            var weapon = Progression.WeaponTier();
            var suit = Progression.ArmorTier(c.Crafted);
            maxHp += Progression.ArmorTiers[suit - 1].Hp;
            armor += Progression.ArmorTiers[suit - 1].Armor;
            if (Progression.HasTech(c.Level, c.Techs, TechIds.PlasmaGrenades)) mods = Content.AddMods(mods, new UnitMods { GrenadeDamage = 2 });
            if (Progression.HasTech(c.Level, c.Techs, TechIds.NanoMedkit))
            {
                mods = Content.AddMods(mods, new UnitMods { Heal = 2 });
                // The belt's Botiquín: one more use.
                if (charges.ContainsKey(ChargeIds.FirstAid)) charges[ChargeIds.FirstAid] += 1;
            }
            var traits = s.Traits.Select((t) => Quests.Traits[t]).ToList();
            mods = Content.AddMods(mods, new UnitMods { Aim = traits.Sum((t) => t.Aim) + RangeAim(c) });
            return new SquadMember
            {
                CampaignId = s.Id,
                Name = s.Name,
                Nickname = s.Nickname,
                Appearance = CopyAppearance(s.Appearance),
                WeaponTier = weapon,
                ArmorTier = suit,
                Owner = s.Owner,
                Template = stats.Template,
                Rank = stats.Rank,
                Perks = stats.Perks,
                Mods = mods,
                MaxHp = maxHp,
                Hp = maxHp,
                Charges = charges,
                // Hungry, nobody fights as well (docs/FASE4.md).
                Will = stats.Will + traits.Sum((t) => t.Will) + MemorialWill(c) - (c.Hungry == true ? HungryWill : 0),
                Armor = armor,
                Gear = gear,
                WeaponName = WeaponLabel(c, s),
                Arms = arms,
                Mags = Progression.SpareMags(c.Crafted),
                // The Chapistería: one more backpack slot (docs/FASE8.md §C).
                PackSlots = FacilityLevel(c, FacilityIds.Garage) != 0 ? Content.BackpackSlots + 1 : (int?)null,
                // Its bars fill out there, with what the Taller de oficios adds (docs/FASE10.md §2).
                Lines = Bars.MissionLinesOf(s.Skills, s.Growth, LearningBonus(c)),
            };
        }

        /// <summary>`{ ...a }`.</summary>
        private static Appearance CopyAppearance(Appearance a)
        {
            return new Appearance { Primary = a.Primary, Secondary = a.Secondary, Visor = a.Visor, Head = a.Head, Build = a.Build, Sex = a.Sex, Helmet = a.Helmet, Torso = a.Torso, Arms = a.Arms, Legs = a.Legs, Character = a.Character };
        }

        /// <summary>Medicines also stop a bite's contagion in the missions (docs/FASE8.md §C): the Enfermería from level 2.</summary>
        public static bool MedicineCures(CampaignState c)
        {
            return FacilityLevel(c, FacilityIds.Infirmary) >= 2;
        }

        public static MissionOffer? ActiveOffer(CampaignState c)
        {
            return c.ActiveMission == null ? null : c.Offers.FirstOrDefault((o) => o.Id == c.ActiveMission);
        }

        public static List<SquadMember> BuildSquad(CampaignState c)
        {
            return c.Squad
                .Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id))
                .Where((s) => s != null && SoldierReady(s))
                .Select((s) => SquadMember(c, s!))
                .ToList();
        }

        /// <summary>Campaign bonus to hacking (alien encryption).</summary>
        public static int CampaignHackBonus(CampaignState c)
        {
            return Progression.HasTech(c.Level, c.Techs, TechIds.Encryption) ? 20 : 0;
        }

        /// <summary>
        /// The next level of the Bastion (Arturo, 18/10 #40): what it costs, the
        /// missions it asks to have flown and how many have been; null at the top.
        /// </summary>
        public static LevelStep? NextLevel(CampaignState c)
        {
            var level = Progression.BastionLevel(c.Level) + 1;
            if (level > Progression.MaxLevel) return null;
            return new LevelStep { Level = level, Cost = Progression.LevelCost[level - 1], Missions = Progression.LevelMissions[level - 1], Flown = c.MissionsFlown ?? 0 };
        }

        /// <summary>Why the Bastion can't go up a level now, or null.</summary>
        public static string? LevelUpBlocker(CampaignState c)
        {
            var next = NextLevel(c);
            if (next == null) return "El Bastión ya está en su nivel más alto.";
            if (next.Flown < next.Missions) return $"Para {Lore.BastionLevelThe(next.Level)} hacen falta {next.Missions} misiones hechas: lleváis {next.Flown}.";
            return CostBlocker(c, next.Cost, 0);
        }
    }
}
