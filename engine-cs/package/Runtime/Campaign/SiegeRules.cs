#nullable enable
// campaign.ts: the sieges as they come and go (docs/FASE12.md §11.5): their sides, the next one, the one that has come,
// what it left of the base, and its fall.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ---------------------------------------------------------------- the sieges

        /// <summary>The sides a siege comes by: three or all four (Ana, 23/10: «que lleguen de todos lados»), at random.</summary>
        private static List<int> SiegeSides(Rng rng)
        {
            var sides = new List<int> { 0, 1, 2, 3 };
            for (var i = sides.Count - 1; i > 0; i--)
            {
                var j = rng.Int(i + 1);
                (sides[i], sides[j]) = (sides[j], sides[i]);
            }
            // Sorted as JavaScript sorts numbers by default, as text: one digit each, so the same.
            return sides.Take(3 + rng.Int(2)).OrderBy((x) => x).ToList();
        }

        /// <summary>The next siege, once one is over: a few missions off, bigger than the last.</summary>
        private static SiegeClock NextSiege(CampaignState c, Rng rng)
        {
            var held = c.Siege.Held;
            return new SiegeClock { In = c.Doom * 2 >= DoomMax ? SiegeEvery - 1 : SiegeEvery, Count = SiegeFirst + held * SiegeGrowth + c.Doom, Sides = SiegeSides(rng), Held = held };
        }

        /// <summary>
        /// The siege has come (§11.5): it is the next thing the base does, and every
        /// soldier fit to fight defends; the wounded and the bitten stay inside.
        /// </summary>
        private static void StartSiege(CampaignState c, Rng rng)
        {
            var night = c.Siege.Held > 0 && rng.Int(SiegeNight) == 0;
            var offer = new MissionOffer
            {
                Id = NewId(c, "o"),
                Name = "Asedio",
                Kind = MissionKinds.Elimination,
                Difficulty = Math.Min(4, 1 + c.Siege.Held / 2),
                ExpiresDay = null,
                Reward = new MissionReward(),
                // Fought on the base as it stands when it starts (siegeMap): this one is never used.
                Map = MapSource.Handmade("bastion"),
                Pods = new List<List<string>>(),
                Final = false,
                Region = HomeRegion,
                Facility = null,
                Story = null,
                Quest = null,
                Siege = true,
                Night = night ? true : (bool?)null,
            };
            c.Offers.Add(offer);
            c.ActiveMission = offer.Id;
            c.Squad = c.Soldiers.Where(SoldierReady).Select((s) => s.Id).ToList();
            c.SiegePosts = new Dictionary<string, Vec2>();
            c.LaunchReady = new bool[2];
            SyncPads(c);
            Log(c, Lore.BaseSiegeText.Arrival, LogTones.Bad, Alert("Asedio", Lore.BaseSiegeText.Here));
        }

        /// <summary>
        /// What a siege left of the base (§11.4): each piece as beaten as the worst of
        /// its tiles, the traps with the uses they have left, the turrets as they ended.
        /// What broke stays broken until it is mended.
        /// </summary>
        private static SiegeDamage TakeBackBase(CampaignState c, GameState s)
        {
            var damage = new SiegeDamage();
            if (s.Base == null) return damage;
            foreach (var p in c.Bastion.Pieces)
            {
                if (!s.Base.Pieces.Any((q) => q.Id == p.Id)) continue;
                var hp = p.Hp;
                var charges = p.Charges;
                // The caravan stands for the Bastion's building, all of it, as baseLayout put it up: hits past the caravan's own 4×2 count too.
                var cells = p.Piece == PieceIds.Caravan ? Base.BastionFootprint(c.Bastion) : Base.CellsOf(p).Cast<Vec2>().ToList();
                foreach (var cell in cells)
                {
                    if (s.Breakables != null && s.Breakables.TryGetValue(cell.Y * s.Width + cell.X, out var left)) p.Hp = Math.Min(p.Hp, left);
                }
                var trap = s.Traps?.FirstOrDefault((t) => t.Piece == p.Id);
                if (trap != null) p.Charges = trap.Charges;
                var turret = s.Units.FirstOrDefault((u) => u.Automaton?.Piece == p.Id);
                if (turret != null)
                {
                    p.Hp = turret.Alive ? turret.Hp : 0;
                    p.Charges = turret.Ammo;
                }
                if (p.Hp < hp)
                {
                    if (p.Hp <= 0) damage.Broken++;
                    else damage.Hurt++;
                }
                if ((p.Charges ?? 0) < (charges ?? 0)) damage.Spent++;
            }
            return damage;
        }

        /// <summary>
        /// The base has fallen (§11.5): whoever is left flees to another clearing with
        /// the caravan and what they stand in. What was built and the stores are lost;
        /// the soldiers keep what they learnt and carry.
        /// </summary>
        private static void FallBase(CampaignState c, Rng rng)
        {
            // The level stays: the new clearing has the building of its stage.
            var clearing = Base.FreshBase(rng);
            clearing.Level = Progression.BastionLevel(c);
            c.Bastion = clearing;
            c.Credits = 0;
            c.Parts = 0;
            c.Medicines = 0;
            c.Food = StartFood / 2;
            // Only what someone carries: the rest of the Armería stays behind.
            var alive = c.Soldiers.Where((s) => s.Alive).ToList();
            var carried = new Dictionary<string, int>();
            foreach (var s in alive)
            {
                foreach (var id in new[] { s.Loadout.Utility, s.Loadout.Ammo })
                    if (!string.IsNullOrEmpty(id)) carried[id!] = (carried.TryGetValue(id!, out var n) ? n : 0) + 1;
            }
            c.Inventory = carried;
            c.FoundWeapons = (c.FoundWeapons ?? new List<FoundWeapon>()).Where((w) => !string.IsNullOrEmpty(w.Holder) && alive.Any((s) => s.Id == w.Holder)).ToList();
            c.WeaponMods = new Dictionary<string, int>();
            c.Crafted = new List<string>();
            c.Base.Works = new List<Construction>();
            Log(c, $"{Lore.BaseSiegeText.Fallen}. {Lore.BaseSiegeText.FallenLine}", LogTones.Bad, Alert("Asedio", Lore.BaseSiegeText.Fallen));
        }
    }
}
