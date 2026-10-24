#nullable enable
// campaign.ts: time (advanceTime, passDays, passDay): a day in the Bastion, with everything that comes with it. Texts
// fixed as text-fixes.ts has them (Gameplay and Lore, 24/10): the convoy's line, the defeat's.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // --------------------------------------------------------------------- time

        /// <summary>Advances day by day until something worth stopping for happens.</summary>
        public static void AdvanceTime(CampaignState c, Rng rng)
        {
            // Catches up with story beats that had to wait (another event was pending).
            RefreshNarrative(c, rng);
            for (var i = 0; i < 40 && c.Outcome == null && c.Event == null; i++)
            {
                if (PassDay(c, rng)) return;
            }
        }

        /// <summary>Lets `days` go by whatever happens on them (a mission's flight and recovery).</summary>
        private static void PassDays(CampaignState c, int days, Rng rng)
        {
            for (var i = 0; i < days && c.Outcome == null; i++) PassDay(c, rng);
        }

        /// <summary>One day in the Bastion. True when something happened that should stop the clock.</summary>
        private static bool PassDay(CampaignState c, Rng rng)
        {
            c.Day++;
            var stop = false;

            // Hungry, wounds heal only every other day.
            var heal = c.Hungry == true && c.Day % 2 == 1 ? 0 : HealRate(c);
            foreach (var s in c.Soldiers)
            {
                if (!s.Alive || s.WoundedDays == 0 || heal == 0) continue;
                s.WoundedDays = Math.Max(0, s.WoundedDays - heal);
                if (s.WoundedDays == 0) Log(c, $"{s.Name} se ha recuperado de sus heridas.", LogTones.Good);
            }

            foreach (var work in c.Base.Works.ToList())
            {
                if (--work.DaysLeft > 0) continue;
                var def = Progression.Crafts[work.Project!];
                if (!string.IsNullOrEmpty(def.Makes))
                {
                    (c.FoundWeapons ??= new List<FoundWeapon>()).Add(new FoundWeapon { Id = NewId(c, "w"), Kind = def.Makes!, Holder = null });
                    Log(c, $"El Taller ha terminado {def.The}. Ya está en la Armería.", LogTones.Good);
                }
                else if (!string.IsNullOrEmpty(def.MakesMod))
                {
                    var mods = c.WeaponMods ??= new Dictionary<string, int>();
                    mods[def.MakesMod!] = (mods.TryGetValue(def.MakesMod!, out var n) ? n : 0) + 1;
                    Log(c, $"El Taller ha terminado {def.The}. Ya está en la Armería, para montar en un arma.", LogTones.Good);
                }
                else
                {
                    (c.Crafted ??= new List<string>()).Add(def.Id);
                    if (def.Kind == CraftKinds.Ammo) Log(c, $"El Taller ha terminado {def.The}: un cargador de repuesto más para cada soldado que salga.", LogTones.Good);
                    else Log(c, $"El Taller ha terminado: {def.Name.ToLowerInvariant()} para todos.", LogTones.Good);
                }
                c.Base.Works = c.Base.Works.Where((w) => w != work).ToList();
                stop = true;
            }

            foreach (var o in c.Offers.ToList())
            {
                if (o.ExpiresDay == null || o.ExpiresDay >= c.Day || o.Id == c.ActiveMission) continue;
                c.Offers = c.Offers.Where((x) => x.Id != o.Id).ToList();
                var q = QuestOf(c, o);
                if (q != null)
                {
                    // A side quest's operation that expires loses the quest instead.
                    Log(c, $"La misión {o.Name} ha expirado.", LogTones.Bad);
                    FinishQuest(c, q, "failure", rng);
                }
                else
                {
                    c.Doom++;
                    Log(c, $"La misión {o.Name} ha expirado. {Lore.DoomChangeText(Progression.BastionLevel(c), 1)}", LogTones.Bad);
                }
                stop = true;
            }

            var listened = c.Rumors.FirstOrDefault((r) => r.Id == c.Listening);
            if (listened != null && --listened.DaysLeft <= 0)
            {
                ResolveRumor(c, listened, rng);
                stop = true;
            }
            foreach (var r in c.Rumors.Where((x) => x.Id != c.Listening && x.ExpiresDay < c.Day).ToList())
            {
                c.Rumors = c.Rumors.Where((x) => x.Id != r.Id).ToList();
                Log(c, $"El rumor {Lore.OfZone(Regions[r.Region].Name)} se ha enfriado.");
            }
            if (c.Day >= c.NextRumorDay)
            {
                c.NextRumorDay = c.Day + 1 + (rng.Int(5) >= 2 ? 1 : 0);
                // Rumours are heard on the radio: none without a Sala de radio (Ana, 23/10 #28).
                if (c.Rumors.Count < MaxRumors && HasRadio(c))
                {
                    NewRumor(c, rng);
                    stop = true;
                }
            }

            if (c.Day >= c.NextMissionDay)
            {
                c.NextMissionDay = c.Day + 1 + (rng.Int(3) == 2 ? 1 : 0);
                if (c.Offers.Count((o) => !o.Final && o.Facility == null && (o.Story ?? 0) == 0) < MaxOffers + (FacilityLevel(c, FacilityIds.Listening) != 0 ? 1 : 0))
                {
                    var offer = VehicleDue(c) && rng.Int(3) == 0 ? VehicleOffer(c, rng) : MakeOffer(c, rng);
                    c.Offers.Add(offer);
                    Log(c, $"Nueva misión en {Regions[offer.Region].Name}: {offer.Name}.");
                    stop = true;
                }
            }

            if (c.Day >= c.NextDoomDay)
            {
                c.NextDoomDay = c.Day + DoomInterval + ListeningDelay(c);
                c.Doom++;
                Log(c, Lore.DoomChangeText(Progression.BastionLevel(c), 1), LogTones.Bad);
                stop = true;
            }

            foreach (var r in c.Regions)
            {
                if (r.Facility == null || c.Day < r.Facility.NextDoomDay) continue;
                r.Facility.NextDoomDay = c.Day + FacilityDoomInterval;
                c.Doom++;
                var level = Progression.BastionLevel(c);
                Log(c, level >= Lore.PartituraLevel ? $"El Nido {Lore.OfZone(Regions[r.Id].Name)} acerca la Marea." : $"El Nido {Lore.OfZone(Regions[r.Id].Name)} hace crecer {Lore.DoomSubject(level)}.", LogTones.Bad);
                stop = true;
            }

            if (c.Day >= c.NextFacilityDay)
            {
                c.NextFacilityDay = c.Day + 4 + (rng.Int(7) >= 4 ? 1 : 0);
                var free = c.Regions.Where((r) => r.Facility == null).ToList();
                if (c.Regions.Count((r) => r.Facility != null) < MaxFacilities && free.Count > 0)
                {
                    // Nidos gather under the Marea or beside it, three times as often as elsewhere.
                    int Weight(RegionState r) => UnderMarea(c, r.Id) || Regions[r.Id].Roads.Any((n) => UnderMarea(c, n)) ? NidoTideWeight : 1;
                    var roll = rng.Int(free.Sum(Weight));
                    var region = free.First((r) => (roll -= Weight(r)) < 0);
                    region.Facility = new RegionFacility { NextDoomDay = c.Day + FacilityDoomInterval };
                    Log(c, $"Los zombis se juntan en un Nido en {Regions[region.Id].Name}.", LogTones.Bad);
                    if (region.Contacted) c.Offers.Add(MakeFacilityOffer(c, region.Id, rng));
                    else Log(c, $"Contactad con {Regions[region.Id].Name} para poder asaltarlo.");
                    stop = true;
                }
            }

            // The bitten whose days ran out turn.
            foreach (var s in c.Soldiers)
            {
                var day = s.Alive ? TurnDay(c, s) : null;
                if (day == null || c.Day < day.Value) continue;
                Turn(c, s);
                stop = true;
            }

            // Food (docs/FASE4.md): the Huerto grows, La Vega sends, and the Bastion eats.
            AddFood(c, GardenYield(c));
            if (c.Day >= (c.NextFoodDay ?? 0))
            {
                c.NextFoodDay = c.Day + LaVegaFoodDays;
                if (EnclaveStands(c, RegionIds.Oce))
                {
                    AddFood(c, LaVegaFood);
                    Log(c, $"{Regions[RegionIds.Oce].Name} manda comida: +{LaVegaFood} raciones.", LogTones.Good);
                }
            }
            var meal = DailyRations(c);
            if ((c.Food ?? 0) >= meal)
            {
                c.Food = (c.Food ?? 0) - meal;
                // The day, always in view (Ana and Arturo, 22/10), with what it ate.
                if (meal != 0) Log(c, $"Día {c.Day}: se {(meal == 1 ? "come 1 ración" : $"comen {meal} raciones")}.");
            }
            else
            {
                c.Food = 0;
                if (c.Hungry != true)
                {
                    c.Hungry = true;
                    Log(c, "No quedan raciones: el Bastión pasa hambre. Hasta que vuelva a haberlas, −10 de voluntad en las misiones y las heridas curan más despacio.", LogTones.Bad, Alert("Despensa", "Hambre en el Bastión"));
                    stop = true;
                }
            }

            // The enclaves that still stand send medicines.
            if (c.Day >= (c.NextMedicineDay ?? 0))
            {
                c.NextMedicineDay = c.Day + EnclaveDays;
                var enclaves = LivingEnclaves(c);
                if (enclaves.Count > 0)
                {
                    c.Medicines = (c.Medicines ?? 0) + enclaves.Count;
                    Log(c, $"{string.Join(" y ", enclaves.Select((id) => Regions[id].Name))} {(enclaves.Count > 1 ? "mandan" : "manda")} medicinas: +{enclaves.Count} ✚.", LogTones.Good);
                }
            }

            if (c.Day >= c.NextSupplyDay)
            {
                c.NextSupplyDay = c.Day + Month;
                var income = ConvoyIncome(c);
                c.Credits += income;
                // «Llega el convoy de Bielsa: Ruth Halvorsen manda N suministros.» (text-fixes.ts, Gameplay 24/10): it brings what every contacted zone pays.
                Log(c, $"Llega el convoy de Ruth Halvorsen: {income} suministros de lo pactado con los enclaves.", LogTones.Good);
                stop = true;
            }

            if (c.Day >= c.NextEventDay)
            {
                c.NextEventDay = c.Day + 2 + (rng.Int(5) >= 3 ? 1 : 0);
                // What comes by radio, only with a Sala de radio to hear it (Ana, 23/10 #27).
                var id = rng.Pick(Events.Keys.Where((x) => Events[x].Story != true && (Events[x].Radio != true || HasRadio(c))).ToList());
                c.Event = id;
                c.Seen[id] = (c.Seen.TryGetValue(id, out var times) ? times : 0) + 1;
                Log(c, $"Acontecimiento: {Events[id].Title}.");
                stop = true;
            }

            if (SyncMarea(c)) stop = true;
            if (c.Doom >= DoomMax)
            {
                c.Outcome = Outcomes.Defeat;
                // «La Marea ha llegado al Bastión.» (text-fixes.ts, Lore 24/10): before level 4 nobody knows of the Marea.
                Log(c, "Los muertos han llegado al Bastión.", LogTones.Bad);
                return true;
            }
            // A story chapter whose wait is over opens today.
            var offers = c.Offers.Count;
            RefreshNarrative(c, rng);
            return stop || c.Offers.Count > offers || c.Event != null;
        }
    }
}
