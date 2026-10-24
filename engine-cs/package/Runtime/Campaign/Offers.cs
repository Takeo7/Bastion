#nullable enable
// campaign.ts: the Marea moving over the offers (tide, syncMarea) and the making of the operations: the ordinary ones,
// a Nido's assault, a story chapter's, a better vehicle's, the story opening its next beat, and the tip-offs.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ----------------------------------------------------------------- the Marea

        /// <summary>The group of the dead a mission under the Marea adds.</summary>
        private static List<string> TidePod(int difficulty)
        {
            return difficulty >= 3 ? new List<string> { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Xenoid } : new List<string> { TemplateIds.Zombie, TemplateIds.Zombie };
        }

        /// <summary>A mission whose zone is under the Marea: one more group of the dead (the last); out of it, back as it was.</summary>
        private static void Tide(CampaignState c, MissionOffer o)
        {
            if (o.Final) return;
            var flooded = UnderMarea(c, o.Region);
            if (flooded && o.Tide != true)
            {
                o.Pods = new List<List<string>>(o.Pods) { TidePod(o.Difficulty) };
                o.Tide = true;
            }
            else if (!flooded && o.Tide == true)
            {
                o.Pods = o.Pods.Take(o.Pods.Count - 1).ToList();
                o.Tide = null;
            }
        }

        /// <summary>
        /// Looks where the Marea is now (it follows the activity): an alert for every
        /// zone it reaches or frees, and the missions there made harder or not.
        /// Whether any zone changed.
        /// </summary>
        private static bool SyncMarea(CampaignState c)
        {
            var now = FloodedZones(c);
            var before = c.Flooded ?? new List<string>();
            // Before the Bastion's level 4 nobody knows the word «Marea»: Lore's lines say it as they can.
            void Say(string id, bool reached)
            {
                var a = Lore.MareaAlert(Progression.BastionLevel(c), Regions[id].Name, reached);
                Log(c, a.Text, reached ? LogTones.Bad : LogTones.Good, Alert(a.Kicker, a.Title));
            }
            foreach (var id in now.Where((x) => !before.Contains(x)).ToList()) Say(id, true);
            foreach (var id in before.Where((x) => !now.Contains(x)).ToList()) Say(id, false);
            c.Flooded = now;
            foreach (var o in c.Offers) Tide(c, o);
            return now.Count != before.Count || now.Any((x) => !before.Contains(x));
        }

        // ----------------------------------------------------------------- missions

        /// <summary>Mission types offered by the resistance network, with their relative frequency.</summary>
        private static readonly IReadOnlyList<(string Kind, int Weight)> OfferKinds = new[]
        {
            (MissionKinds.Elimination, 3),
            (MissionKinds.Recovery, 2),
            (MissionKinds.Hack, 2),
            (MissionKinds.Rescue, 2),
            (MissionKinds.Sabotage, 1),
            (MissionKinds.Scavenge, 2),
        };

        private static string PickKind(Rng rng)
        {
            var total = OfferKinds.Sum((k) => k.Weight);
            var roll = rng.Int(total);
            foreach (var (kind, w) in OfferKinds)
            {
                if (roll < w) return kind;
                roll -= w;
            }
            return MissionKinds.Elimination;
        }

        /// <summary>Difficulty of a new operation: it grows with the days and, a little, with the missions flown (second playtest).</summary>
        private static int DifficultyNow(CampaignState c, Rng rng)
        {
            return Math.Min(4, 1 + (int)Math.Floor(c.Day * OldDaysPerMission / 18.0 + (c.MissionsFlown ?? 0) / 10.0) + (rng.Int(4) == 0 ? 1 : 0));
        }

        /// <summary>
        /// The enemy groups of an operation, with what side quests left in its region
        /// (missionPods: a Saqueo has the dead scattered about; a mission of the
        /// living, their bands among the dead). The veterans' extra bodies are of
        /// whoever holds it.
        /// </summary>
        private static List<List<string>> RegionPods(CampaignState c, string region, int difficulty, string kind, bool living = false)
        {
            var pods = Setup.MissionPods(difficulty, kind, living);
            var extra = RegionEnemy(c, region);
            if (extra != null) pods[0].Add(extra);
            var veteran = kind == MissionKinds.Scavenge ? TemplateIds.Zombie : living ? (difficulty >= 3 ? TemplateIds.Lancer : TemplateIds.Trooper) : difficulty >= 3 ? TemplateIds.Xenoid : TemplateIds.Zombie;
            for (var i = 0; i < VeteranEnemies(c); i++) pods[(i + 1) % pods.Count].Add(veteran);
            return pods;
        }

        private static readonly IReadOnlyList<string> OfferBiomes = new[] { Biomes.City, Biomes.City, Biomes.Wilds };
        private static readonly IReadOnlyList<string> SabotageAlarms = new[] { Alarms.Factory, Alarms.Factory, Alarms.Police, Alarms.Ambulance };

        private static MissionOffer MakeOffer(CampaignState c, Rng rng, bool final = false, string? at = null)
        {
            var difficulty = final ? 4 : DifficultyNow(c, rng);
            var kind = final ? MissionKinds.Elimination : PickKind(rng);
            // Relays live in alien facilities, Saqueos in towns; everything else happens in towns or the countryside.
            var biome = final || kind == MissionKinds.Sabotage ? Biomes.Facility : kind == MissionKinds.Scavenge ? Biomes.City : rng.Pick(OfferBiomes);
            var contacted = c.Regions.Where((r) => r.Contacted).Select((r) => r.Id).ToList();
            string region;
            if (at != null) region = at;
            else region = rng.Int(10) < 7 && contacted.Count > 0 ? rng.Pick(contacted) : rng.Pick(RegionIds.All);
            // Data and relays are worth more to the Council than a firefight; a Saqueo pays in what it brings back.
            var bonus = kind == MissionKinds.Recovery || kind == MissionKinds.Hack || kind == MissionKinds.Sabotage ? 20 : kind == MissionKinds.Scavenge ? -30 : 0;
            var reward = final ? new MissionReward() : new MissionReward { Credits = 50 + difficulty * 25 + bonus + rng.Int(4) * 5 };
            if (!final && (kind == MissionKinds.Rescue || rng.Int(5) == 0))
            {
                // The roll that once picked the recruit's class stays, so the offers after it come out as before.
                rng.Int(4);
                reward.Recruit = true;
            }
            if (!final && (kind == MissionKinds.Sabotage || (kind == MissionKinds.Hack && rng.Int(3) == 0))) reward.Doom = 1;
            var seed = Seed31(rng);
            // Who a rescue is for and what a sabotage silences, chosen now so the report can say it (playtest 2026-10-15, #14, #20).
            var vip = kind == MissionKinds.Rescue && !final ? Setup.VipNames[seed % Setup.VipNames.Count] : null;
            var alarm = kind == MissionKinds.Sabotage ? SabotageAlarms[seed % 4] : null;
            // The scenarios (playtest 2026-10-14, #25), by the map's seed: half the countryside is a service area on the
            // forest road, a third of the towns an industrial estate.
            var site = final ? null : biome == Biomes.Wilds && seed % 2 == 0 ? Sites.Roadside : biome == Biomes.City && seed % 3 == 0 ? Sites.Industrial : null;
            // An ordinary rescue is someone the Cuervos hold, and the last mission is the Congregación's camp (docs/ZOMBIS-PRIMERO.md); every other operation is the dead's.
            var living = final || kind == MissionKinds.Rescue;
            var id = NewId(c, "m");
            return ByNight(WithProvisions(WithMedicines(new MissionOffer
            {
                Id = id,
                // An ordinary operation is named for where it goes (Lore): «El colegio de Tarbes».
                Name = final ? "El campamento de la sierra" : Lore.MissionName(biome, Regions[region].Name, seed, site),
                Kind = kind,
                Difficulty = difficulty,
                // Operations don't expire (Arturo, 18/10 #39): they wait, and more come.
                ExpiresDay = null,
                Reward = reward,
                Map = MapSource.Generated(biome, seed, site),
                Pods = RegionPods(c, region, difficulty, kind, living),
                Final = final,
                Region = region,
                Facility = null,
                Story = null,
                Quest = null,
                Living = living ? true : (bool?)null,
                Vip = vip,
                Alarm = alarm,
            })));
        }

        /// <summary>Rations and parts some operations pay (docs/FASE4.md), by seed: one in three pays rations, one in four parts.</summary>
        private static MissionOffer WithProvisions(MissionOffer o)
        {
            if (o.Final || o.Map.Kind != MapSourceKinds.Generated) return o;
            var roll = unchecked(((uint)(o.Map.Seed ?? 0) ^ 0x666f6f64u) * 0x2c1b3c6du);
            var reward = CopyReward(o.Reward);
            if (roll % 3 == 0) reward.Food = RewardFood;
            if ((roll >> 8) % 4 == 0) reward.Parts = RewardParts;
            var copy = CopyOffer(o);
            copy.Reward = reward;
            return copy;
        }

        /// <summary>Some operations pay medicines too (docs/FASE3.md): the recoveries (a pharmacy, a courier's bag) and one in four of the rest, by seed.</summary>
        private static MissionOffer WithMedicines(MissionOffer o)
        {
            if (o.Final || o.Map.Kind != MapSourceKinds.Generated) return o;
            var lucky = unchecked(((uint)(o.Map.Seed ?? 0) ^ 0x6d656473u) * 0x27d4eb2du) % 4 == 0;
            if (o.Kind != MissionKinds.Recovery && !lucky) return o;
            var reward = CopyReward(o.Reward);
            reward.Medicines = 1;
            var copy = CopyOffer(o);
            copy.Reward = reward;
            return copy;
        }

        private static MissionOffer MakeFacilityOffer(CampaignState c, string region, Rng rng)
        {
            var difficulty = Math.Min(4, DifficultyNow(c, rng) + 1);
            var id = NewId(c, "m");
            var name = $"Nido {Lore.OfZone(Regions[region].Name)}";
            var seed = Seed31(rng);
            return ByNight(new MissionOffer
            {
                Id = id,
                Name = name,
                Kind = MissionKinds.Sabotage,
                Difficulty = difficulty,
                ExpiresDay = null,
                Reward = new MissionReward { Credits = 120 + difficulty * 20, Doom = 2 },
                Map = MapSource.Generated(Biomes.Facility, seed),
                Pods = Setup.PodPlan(difficulty),
                Final = false,
                Region = region,
                Facility = region,
                Story = null,
                Quest = null,
                // A Nido's noise is a factory's alarm (Lore, #14).
                Alarm = Alarms.Factory,
            });
        }

        /// <summary>The mission of a story chapter: fixed place and type, and it never expires.</summary>
        private static MissionOffer MakeStoryOffer(CampaignState c, int chapter, Rng rng)
        {
            var def = Lore.Story[chapter - 1];
            // Without Simão inside, the Archive is harder to crack.
            var difficulty = Math.Min(4, def.Difficulty + (chapter == Lore.SimonChapter && c.Story.Simon == Lore.SimonChoices.Refused ? 1 : 0));
            var id = NewId(c, "m");
            var seed = Seed31(rng);
            return new MissionOffer
            {
                Id = id,
                Name = def.Title,
                Kind = def.Kind,
                Difficulty = difficulty,
                ExpiresDay = null,
                Reward = CopyReward(def.Reward),
                Map = MapSource.Generated(def.Biome, seed),
                Pods = Setup.MissionPods(difficulty, def.Kind, def.Living == true),
                Final = false,
                Region = def.Region,
                Facility = null,
                Story = chapter,
                Quest = null,
                Living = def.Living == true ? true : (bool?)null,
                // The Cuervos take their sirens from patrol cars (Lore, #14).
                Alarm = def.Kind == MissionKinds.Sabotage ? Alarms.Police : null,
            };
        }

        /// <summary>
        /// Opens whatever the story has ready: the next chapter's mission once the
        /// Bastion reaches its level, Simão's visit before chapter 5 and, with every
        /// chapter won and the top level reached, the final mission.
        /// </summary>
        private static void RefreshStory(CampaignState c, Rng rng)
        {
            if (c.Outcome != null) return;
            var next = c.Story.Done + 1;
            if (next <= Lore.Story.Count && Progression.BastionLevel(c) >= next && c.Day >= (c.Story.NextDay ?? 0))
            {
                if (next == Lore.SimonChapter && string.IsNullOrEmpty(c.Story.Simon))
                {
                    if (c.Event == null)
                    {
                        c.Event = CampaignEventIds.Simon;
                        Log(c, "Alguien llama a la puerta del Bastión.");
                    }
                }
                else if (!c.Offers.Any((o) => o.Story == next))
                {
                    var offer = MakeStoryOffer(c, next, rng);
                    c.Offers.Add(offer);
                    Log(c, $"Nueva misión de historia: {offer.Name}.", LogTones.Good);
                }
            }
            if (c.Story.Done >= Lore.Story.Count && Progression.HasTech(c, TechIds.Signal) && !c.Offers.Any((o) => o.Final))
            {
                c.Offers.Add(MakeOffer(c, rng, true));
                Log(c, "¡La Lata está lista! La misión final está disponible.", LogTones.Good);
            }
        }

        /// <summary>Opens the story's next beats and the side quests whose time has come.</summary>
        private static void RefreshNarrative(CampaignState c, Rng rng)
        {
            RefreshStory(c, rng);
            RefreshQuests(c, rng);
        }

        /// <summary>The next vehicle can turn up: La Lata isn't the bus yet, none is on offer, and enough missions are flown.</summary>
        private static bool VehicleDue(CampaignState c)
        {
            var next = VehicleOf(c) + 1;
            return next < VehicleSeats.Count && (c.MissionsFlown ?? 0) >= VehicleFrom[next] && !c.Offers.Any((o) => (o.Reward.Vehicle ?? 0) != 0);
        }

        /// <summary>
        /// An operation to bring back a better vehicle (#45): a Limpieza («despejad la
        /// zona y es nuestro»); the bus, always at Atalaya, in Bielsa, a military station.
        /// </summary>
        private static MissionOffer VehicleOffer(CampaignState c, Rng rng)
        {
            var tier = VehicleOf(c) + 1;
            var @base = MakeOffer(c, rng);
            var bus = tier >= VehicleSeats.Count - 1;
            var region = bus ? HomeRegion : @base.Region;
            var map = bus ? MapSource.Generated(Biomes.Facility, Seed31(rng)) : @base.Map;
            var offer = CopyOffer(@base);
            offer.Kind = MissionKinds.Elimination;
            offer.Region = region;
            offer.Map = map;
            offer.Pods = bus ? RegionPods(c, region, @base.Difficulty, MissionKinds.Elimination, false) : @base.Pods;
            offer.Name = Lore.VehicleMissionName(tier, Regions[region].Name);
            var reward = CopyReward(@base.Reward);
            reward.Vehicle = tier;
            offer.Reward = reward;
            return offer;
        }

        /// <summary>
        /// What someone knew (an event, a rumour), now that the Bastion's level is
        /// bought and has no experience (#40): where to go, a new operation; they don't
        /// expire (#39).
        /// </summary>
        private static void TipOff(CampaignState c, Rng rng)
        {
            var offer = MakeOffer(c, rng);
            c.Offers.Add(offer);
            Log(c, $"Nueva misión en {Regions[offer.Region].Name}: {offer.Name}.", LogTones.Good);
        }

        /// <summary>At least two ordinary operations to choose from: time only goes by with the missions now (§11.6).</summary>
        private static void TopUpOffers(CampaignState c, Rng rng)
        {
            while (c.Offers.Count((o) => !o.Final && o.Facility == null && (o.Story ?? 0) == 0 && o.Quest == null && o.Siege != true) < 2) c.Offers.Add(MakeOffer(c, rng));
        }
    }
}
