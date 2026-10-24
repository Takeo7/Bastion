#nullable enable
// campaign.ts: a level reached (reachedLevel), and a finished tactical mission folded back into the campaign
// (applyMissionResult): the squad as it came back, what it brought, what it learnt, the reward, the story and the
// quests, the siege's clock, the bitten.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ------------------------------------------------------------- deployment

        /// <summary>A level reached: said, the weapons it brings to the Armería (docs/FASE8.md §A), and the story it may open.</summary>
        private static void ReachedLevel(CampaignState c, int level, Rng rng)
        {
            Log(c, $"El Bastión ya es {Lore.BastionLevelA(level)}.", LogTones.Good);
            var arms = Progression.LevelArms.TryGetValue(level, out var brought) ? brought : new string[0];
            foreach (var kind in arms) (c.FoundWeapons ??= new List<FoundWeapon>()).Add(new FoundWeapon { Id = NewId(c, "w"), Kind = kind, Holder = null });
            if (arms.Count > 0) Log(c, $"A la Armería {(arms.Count == 1 ? "llega" : "llegan")} {ListOf(arms.Select((k) => Content.FoundWeapons[k].Find).ToList())}.", LogTones.Good);
            RefreshNarrative(c, rng);
        }

        /// <summary>`if (reward.recruit)`: true, or a class's name in the rewards of the 0.7.0 and 0.7.1 (MissionReward.Recruit).</summary>
        private static bool Recruits(MissionReward r)
        {
            return r.Recruit is bool b ? b : r.Recruit is string text ? text != "" : r.Recruit != null;
        }

        /// <summary>The debrief's loot line (lore.ts lootSummary), or null when nothing came back.</summary>
        private static string? LootLine(LootTotals h)
        {
            return Lore.LootSummary(h.Supplies, h.Items, h.Medicines, h.Food, h.Parts, h.Weapons);
        }

        /// <summary>Folds a finished tactical mission back into the campaign.</summary>
        public static MissionReport ApplyMissionResult(CampaignState c, GameState mission, Rng rng)
        {
            var offer = ActiveOffer(c);
            var victory = mission.Outcome == Outcomes.Victory;
            var siege = offer?.Siege == true;
            c.MissionsFlown = (c.MissionsFlown ?? 0) + 1;
            // Time goes by with the missions (docs/FASE12.md §11.6): the days pass while the squad is out, so whoever
            // comes back hurt or bitten starts from today; the mission they flew doesn't expire under them (passDay).
            PassDays(c, DaysPerMission, rng);
            MissionReward? reward = null;
            if (victory && offer != null)
            {
                reward = CopyReward(offer.Reward);
                if ((offer.Reward.Credits ?? 0) != 0) reward.Credits = (int)Js.Round(offer.Reward.Credits!.Value * RewardMultiplier(c));
            }
            var report = new MissionReport
            {
                Day = c.Day,
                MissionName = offer?.Name ?? "Misión",
                Victory = victory,
                Reason = mission.OutcomeReason ?? EndReasons.Wiped,
                Squad = new List<ReportSoldier>(),
                Reward = reward,
                DoomChange = 0,
                Xp = 0,
                LevelFrom = Progression.BastionLevel(c),
                LevelTo = Progression.BastionLevel(c),
                Bodies = 0,
                Salvage = 0,
                Story = offer?.Story,
                Night = mission.Mission.Night == true ? true : (bool?)null,
                Siege = siege ? true : (bool?)null,
            };
            // What the siege left of the base: beaten, broken, traps gone off (docs/FASE12.md §11.4).
            if (siege)
            {
                c.SiegePosts = null;
                // Held, it is what there is to mend; lost, the base is left behind anyway.
                var damage = TakeBackBase(c, mission);
                if (victory && (damage.Broken != 0 || damage.Hurt != 0 || damage.Spent != 0)) report.Damage = damage;
            }

            // Abandoning counts as a retreat: whoever is still standing comes home.
            // Down, or left behind when the bus went (docs/PRUEBA-2026-10-14.md P1): either way, gone.
            bool Fell(Unit u) => (!u.Alive && !u.Evacuated && u.Hp <= 0) || u.Lost == true;
            // The Camposanto at level 3: whoever comes back from a mission with losses learns from it.
            var mourning = FacilityLevel(c, FacilityIds.Memorial) >= 3 && mission.Units.Any((u) => !string.IsNullOrEmpty(u.CampaignId) && Fell(u)) ? 1 : 0;
            // Weapons that changed hands in the mission (docs/FASE8.md §C): what a soldier comes home holding is theirs;
            // their own, swapped for a find, comes back only in a backpack. Armería id → whose it was.
            var swapped = new JsMap<string, string>();
            // A fallen soldier's weapon another brought home (its backpack left where it fell: 18/10 #49) isn't lost.
            var cameHome = new HashSet<string>();
            foreach (var u in mission.Units.Where((x) => Looting.BringsLootHome(mission, x)))
            {
                foreach (var l in u.Backpack ?? new List<Loot>()) if (!string.IsNullOrEmpty(l.WeaponId)) cameHome.Add(l.WeaponId!);
                foreach (var id in new[] { u.Arms?.PrimaryId, u.Arms?.MeleeId }) if (!string.IsNullOrEmpty(id)) cameHome.Add(id!);
            }
            foreach (var u in mission.Units)
            {
                var s = !string.IsNullOrEmpty(u.CampaignId) ? c.Soldiers.FirstOrDefault((x) => x.Id == u.CampaignId) : null;
                if (s == null) continue;
                var died = Fell(u);
                s.Missions++;
                s.Kills += u.Kills;
                var entry = new ReportSoldier { Id = s.Id, Name = s.Name, Owner = s.Owner, Status = ReportStatuses.Ok, WoundedDays = 0, Kills = u.Kills };
                report.Squad.Add(entry);
                if (died)
                {
                    s.Alive = false;
                    s.FellDay = report.Day;
                    entry.Status = u.Lost == true ? ReportStatuses.Lost : ReportStatuses.Dead;
                    if (u.Lost == true) Log(c, u.Infected == true && !HasQuarantine(c) ? $"{s.Name} se quedó fuera: llevaba un mordisco, y sin Cuarentena no se sube a La Lata." : $"{s.Name} se quedó fuera cuando salió La Lata.", LogTones.Bad);
                    else if (FacilityCount(c, FacilityIds.Memorial) != 0) Log(c, $"{s.Name} descansa en el Camposanto.");
                    // Equipment is lost with its bearer, the Armería's weapons too.
                    foreach (var item in new[] { s.Loadout.Utility, s.Loadout.Ammo })
                        if (!string.IsNullOrEmpty(item)) c.Inventory[item!] = Math.Max(0, (c.Inventory.TryGetValue(item!, out var n) ? n : 0) - 1);
                    s.Loadout = new Loadout();
                    foreach (var carried in (c.FoundWeapons ?? new List<FoundWeapon>()).Where((w) => w.Holder == s.Id).ToList())
                    {
                        if (cameHome.Contains(carried.Id))
                        {
                            carried.Holder = null;
                            continue;
                        }
                        c.FoundWeapons = c.FoundWeapons!.Where((w) => w != carried).ToList();
                        Log(c, $"Con {s.Name} se pierde su {Content.FoundWeapons[carried.Kind].Name.ToLowerInvariant()}.", LogTones.Bad);
                    }
                    continue;
                }
                if (u.Arms != null)
                {
                    foreach (var primaryHand in new[] { true, false })
                    {
                        var kind = primaryHand ? u.Arms.Primary : u.Arms.Melee;
                        var id = primaryHand ? u.Arms.PrimaryId : u.Arms.MeleeId;
                        var before = primaryHand ? Progression.FoundWeaponOf(c, s) : Progression.MeleeWeaponOf(c, s);
                        if (before != null && before.Id != id)
                        {
                            before.Holder = null;
                            swapped.Set(before.Id, s.Name);
                        }
                        if (!string.IsNullOrEmpty(kind) && string.IsNullOrEmpty(id))
                        {
                            var picked = new FoundWeapon { Id = NewId(c, "w"), Kind = kind!, Holder = s.Id };
                            if (primaryHand) WearAs(picked, u.Arms.Condition, u.Arms.Mods);
                            (c.FoundWeapons ??= new List<FoundWeapon>()).Add(picked);
                        }
                        // Another's weapon taken up out there (a fallen one's: #49) is this soldier's now.
                        var taken = !string.IsNullOrEmpty(id) && id != before?.Id ? c.FoundWeapons?.FirstOrDefault((w) => w.Id == id) : null;
                        if (taken != null) taken.Holder = s.Id;
                    }
                    // What the shots took off the weapon in the hand (docs/FASE9.md §5) comes home with it, and is told when it shows.
                    var own = !string.IsNullOrEmpty(u.Arms.PrimaryId) ? c.FoundWeapons?.FirstOrDefault((w) => w.Id == u.Arms.PrimaryId) : null;
                    if (own != null) WearAs(own, u.Arms.Condition, u.Arms.Mods);
                    if (!string.IsNullOrEmpty(u.Arms.Primary) && Content.ConditionOf(u) < Content.WornBelow) Log(c, Lore.WornLine(s.Name, u.Arms.Primary!, Content.ConditionOf(u) <= 0), LogTones.Bad);
                }
                var lost = u.MaxHp - u.Hp;
                if (lost > 0)
                {
                    // The Chapistería takes a day (a mission) off each wound a level, one left at least.
                    s.WoundedDays = Math.Max(1, OldDaysToNow(WoundDays + lost * WoundDays) - GarageCut(c).Wounds);
                    entry.Status = ReportStatuses.Wounded;
                    entry.WoundedDays = s.WoundedDays;
                }
                // What it did teaches it (docs/FASE10.md §3): more with the Taller de oficios, and the Camposanto at 3 after losses.
                // The bars filled out there keep what they rolled; the Camposanto's share, known only now, may fill more.
                var taught = Bars.Learn(s.Skills, s.Growth, u.Deeds, rng, LearningBonus(c) + (mourning != 0 ? LearningShare : 0), u.Lines);
                foreach (var bar in taught.Bars) Log(c, Lore.BarLine(s.Name, bar.Skill, bar.Growth), LogTones.Good);
                entry.Learned = taught.Gained;
                var deeds = new Deeds();
                foreach (var d in u.Deeds ?? new Deeds()) deeds[d.Key] = d.Value;
                entry.Deeds = deeds;
                entry.Bars = taught.Bars;
                s.Skills = taught.Skills;
                s.Growth = taught.Growth;
            }

            // What the squad searched and brought back (docs/FASE2.md): a fallen soldier's is lost.
            var haul = Looting.BroughtBack(mission);
            report.Loot = new MissionLoot
            {
                Supplies = haul.Supplies,
                Items = haul.Items,
                Medicines = haul.Medicines,
                Food = haul.Food,
                Parts = haul.Parts,
                Weapons = haul.Weapons,
                Arms = haul.Arms,
                Returned = haul.Returned,
                Mods = haul.Mods,
                Line = LootLine(haul),
            };
            c.Credits += haul.Supplies;
            foreach (var item in haul.Items) c.Inventory[item] = (c.Inventory.TryGetValue(item, out var n) ? n : 0) + 1;
            c.Medicines = (c.Medicines ?? 0) + haul.Medicines;
            AddFood(c, haul.Food);
            c.Parts = (c.Parts ?? 0) + haul.Parts;
            // Found weapons go to the Armería, for a soldier to take; a soldier's own, swapped for a find, goes back to the rack.
            foreach (var found in haul.Arms ?? haul.Weapons.Select((kind) => new CarriedWeapon { Kind = kind }).ToList())
            {
                var w = new FoundWeapon { Id = NewId(c, "w"), Kind = found.Kind, Holder = null };
                WearAs(w, found.Condition, found.Mods);
                (c.FoundWeapons ??= new List<FoundWeapon>()).Add(w);
            }
            foreach (var back in haul.Returned ?? new List<CarriedWeapon>())
            {
                swapped.Delete(back.Id!);
                var home = c.FoundWeapons?.FirstOrDefault((w) => w.Id == back.Id);
                if (home != null) WearAs(home, back.Condition, back.Mods);
                else
                {
                    var w = new FoundWeapon { Id = back.Id!, Kind = back.Kind, Holder = null };
                    WearAs(w, back.Condition, back.Mods);
                    (c.FoundWeapons ??= new List<FoundWeapon>()).Add(w);
                }
            }
            foreach (var mod in haul.Mods ?? new List<string>())
            {
                var mods = c.WeaponMods ??= new Dictionary<string, int>();
                mods[mod] = (mods.TryGetValue(mod, out var n) ? n : 0) + 1;
            }
            if (!string.IsNullOrEmpty(report.Loot.Line)) Log(c, report.Loot.Line!, LogTones.Good);
            foreach (var e in swapped)
            {
                var left = c.FoundWeapons!.First((w) => w.Id == e.Key);
                c.FoundWeapons = c.FoundWeapons!.Where((w) => w != left).ToList();
                Log(c, $"{e.Value} vuelve sin su {Content.FoundWeapons[left.Kind].Name.ToLowerInvariant()}: se quedó en la misión.", LogTones.Bad);
            }

            // Holding the field means the bodies come home, and the black market pays for them (not from a Saqueo: you leave by the evac).
            if (victory && mission.Mission.Kind != MissionKinds.Scavenge)
            {
                foreach (var u in mission.Units)
                {
                    if (u.Alive || u.Objective || !BodyValue.TryGetValue(u.Template, out var value) || value == 0) continue;
                    report.Bodies++;
                    report.Salvage += value;
                }
                // Dr Nwosu knows which tesserae are worth something.
                if (c.Story.Done >= Lore.NwosuChapter) report.Salvage = (int)Js.Round(report.Salvage * 1.25);
                c.Credits += report.Salvage;
                if (report.Bodies != 0) Log(c, $"Registráis {report.Bodies} cuerpos: +{report.Salvage} suministros.", LogTones.Good);
                // The Acorazados' plates and hinges: a part each (docs/FASE4.md).
                report.Scrap = mission.Units.Count((u) => u.Template == TemplateIds.Mec && !u.Alive);
                c.Parts = (c.Parts ?? 0) + report.Scrap;
                if (report.Scrap != 0)
                {
                    Log(c, report.Scrap == 1 ? "Le quitáis el chaleco y el casco a un Acorazado: +1 pieza." : $"Les quitáis el chaleco y el casco a {report.Scrap} Acorazados: +{report.Scrap} piezas.", LogTones.Good);
                }
            }

            if (victory && offer != null && (offer.Reward.Vehicle ?? 0) > VehicleOf(c))
            {
                c.Vehicle = offer.Reward.Vehicle;
                Log(c, Lore.VehicleWonLine(c.Vehicle!.Value, VehicleSeats[c.Vehicle.Value]), LogTones.Good);
            }
            if (victory && offer != null)
            {
                var r = offer.Reward;
                c.Credits += (int)Js.Round((r.Credits ?? 0) * RewardMultiplier(c));
                c.Medicines = (c.Medicines ?? 0) + (r.Medicines ?? 0);
                AddFood(c, r.Food ?? 0);
                c.Parts = (c.Parts ?? 0) + (r.Parts ?? 0);
                if ((r.Doom ?? 0) != 0)
                {
                    c.Doom = Math.Max(0, c.Doom - r.Doom!.Value);
                    report.DoomChange = -r.Doom.Value;
                }
                if (Recruits(r))
                {
                    var s = AddSoldier(c, NeediestPlayer(c), rng);
                    Log(c, $"{s.Name} se une al Bastión como recluta.", LogTones.Good);
                }
                if (offer.Facility != null)
                {
                    var region = c.Regions.FirstOrDefault((x) => x.Id == offer.Facility);
                    if (region != null) region.Facility = null;
                    Log(c, $"Nido {Lore.OfZone(Regions[offer.Facility].Name)} quemado.", LogTones.Good);
                }
                Log(c, $"Misión cumplida: {offer.Name}.", LogTones.Good);
                var quest = QuestOf(c, offer);
                if (quest != null) AdvanceQuest(c, quest, rng);
                if ((offer.Story ?? 0) != 0 && offer.Story > c.Story.Done)
                {
                    c.Story.Done = offer.Story!.Value;
                    c.Story.NextDay = c.Day + StoryGap;
                    Log(c, Lore.Story[offer.Story.Value - 1].After, LogTones.Good);
                }
                // The campaign ends when both keys decide who gets the vaccine.
                if (offer.Final)
                {
                    c.Event = CampaignEventIds.Finale;
                    Log(c, "La sirena de La Lata se lleva a la Marea.", LogTones.Good);
                }
            }
            else
            {
                c.Doom++;
                report.DoomChange = 1;
                Log(c, $"Misión fracasada: {offer?.Name ?? ""}.", LogTones.Bad);
                var quest = offer != null ? QuestOf(c, offer) : null;
                if (quest != null) FinishQuest(c, quest, "failure", rng);
                if (c.Doom >= DoomMax) c.Outcome = Outcomes.Defeat;
            }
            foreach (var e in report.Squad) if (e.Status == ReportStatuses.Dead) Log(c, $"{e.Name} ha caído en combate.", LogTones.Bad);

            // The Bastion's level is bought now (#40): a mission brings it no experience, but the story moves on.
            report.Xp = 0;
            RefreshNarrative(c, rng);
            report.LevelTo = Progression.BastionLevel(c);

            if (!c.Soldiers.Any((s) => s.Alive))
            {
                c.Outcome = Outcomes.Defeat;
                Log(c, siege ? $"{Lore.BaseSiegeText.Nobody}. {Lore.BaseSiegeText.NobodyLine}" : "No quedan soldados. El Bastión ha caído.", LogTones.Bad);
            }
            else if (siege && !victory)
            {
                // The base falls with someone left (the wounded who stayed inside): they flee to another clearing (§11.5).
                FallBase(c, rng);
                report.Fallen = true;
            }
            // The siege clock (§11.5): one mission less to the next; once a siege is over, the next one is set.
            if (siege)
            {
                if (victory) c.Siege.Held++;
                c.Siege = NextSiege(c, rng);
            }
            else c.Siege.In = Math.Max(0, c.Siege.In - 1);

            // Ordinary offers are gone either way; facility assaults, story chapters and the final mission stay until won.
            if (offer != null && ((!offer.Final && offer.Facility == null && (offer.Story ?? 0) == 0) || victory)) c.Offers = c.Offers.Where((o) => o.Id != offer.Id).ToList();
            c.ActiveMission = null;
            c.Squad = new List<string>();
            c.LaunchReady = new bool[2];
            c.LastReport = report;
            MaybeFaces(c, report, rng);
            // The bitten come home with a fever: their days count from their arrival (docs/FASE3.md).
            report.Infected = new List<string>();
            foreach (var u in mission.Units)
            {
                var s = u.Infected == true && !Fell(u) ? c.Soldiers.FirstOrDefault((x) => x.Id == u.CampaignId && x.Alive) : null;
                if (s == null) continue;
                s.Infection = new SoldierInfection { Since = c.Day };
                report.Infected.Add(s.Id);
                Log(c, $"{s.Name} vuelve con un mordisco: tiene fiebre.", LogTones.Bad);
            }
            SyncMarea(c);
            if (c.Outcome == null)
            {
                // Always something to fly (time only goes by with missions), and the next siege announced, or here.
                TopUpOffers(c, rng);
                if (c.Siege.In == 0 && !siege) StartSiege(c, rng);
                else Log(c, HasRadio(c) ? Lore.SiegeWarning(c.Siege.In, c.Siege.Count, c.Siege.Sides) : Lore.SiegeWarning(c.Siege.In), c.Siege.In <= 1 ? LogTones.Bad : LogTones.Info);
            }
            return report;
        }
    }
}
