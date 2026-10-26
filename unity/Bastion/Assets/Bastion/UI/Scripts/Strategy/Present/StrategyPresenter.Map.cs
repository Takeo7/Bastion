using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>What the Mapa has picked, which only this screen knows (screen.ts offer, region).</summary>
    public sealed class MapPick
    {
        /// <summary>The mission whose sheet is open; null, none.</summary>
        public string Offer;
        /// <summary>The zone picked (its sheet, without a mission); null, none.</summary>
        public string Region;
    }

    public sealed partial class StrategyPresenter
    {
        /// <summary>The 0.10.1's difficulties by number (screen.ts DIFFICULTY).</summary>
        static readonly string[] Difficulty = { "", "Moderada", "Difícil", "Muy difícil", "Extrema" };

        /// <summary>The news of each screen this player has seen (navNews); the controller marks a screen's as it opens it.</summary>
        public NewsMarks News = new NewsMarks();

        /// <summary>During a siege, only the siege (geoscape.ts shownOffers; #50): the rest come back after it.</summary>
        public static List<MissionOffer> ShownOffers(CampaignState c)
        {
            var now = Campaign.ActiveOffer(c);
            return now != null && now.Siege == true ? new List<MissionOffer> { now } : c.Offers;
        }

        /// <summary>An operation's colour (geoscape.ts offerTone): the final, a Nido, the story, an errand, the rest.</summary>
        static string OfferTone(MissionOffer o) => o.Final ? "#ff5a8a" : o.Facility != null ? "#ff4a5a" : o.Story != null ? "#4a7dff" : o.Quest != null ? "#2f9e6a" : "#ffb030";

        /// <summary>«Proponer desplegar» with the partner here, «Desplegar» alone (decide).</summary>
        string Decide(string verb) => State.Players.Count > 1 && Partner != null && Partner.Connected ? $"Proponer {verb.ToLowerInvariant()}" : verb;

        /// <summary>What is new on each screen, with a stable key so that seeing it once is enough (navNews): the Mapa's.</summary>
        List<(string key, string label, bool urgent)> MapNews(CampaignState c) =>
            ShownOffers(c).Select(o => ($"offer:{o.Id}", $"Nueva misión: {o.Name}", o.Story != null || o.Final || o.Facility != null))
                .Concat(c.Rumors.Select(r => ($"rumor:{r.Id}", $"Rumor en {Campaign.Regions[r.Region].Name}", false))).ToList();

        /// <summary>The Mapa's news, all seen (seeNews): its number on the tab goes.</summary>
        public void SeeNews(string view)
        {
            if (view != "geoscape" || State == null) return;
            var news = MapNews(State).Select(n => n.key).ToList();
            News.Load(State, news);
            News.See(news, news);
        }

        /// <summary>
        /// The Mapa (renderGeoscape, geoscape.ts): the valley with its zones' marks and La Lata; on the left the missions,
        /// «Esperar un día», the Nidos, the errands, the rumours and the network; on the right a mission's or a zone's sheet.
        /// </summary>
        public MapView Map(MapPick pick)
        {
            pick = pick ?? new MapPick();
            var c = State;
            var level = Level;
            var offers = ShownOffers(c)
                .OrderByDescending(o => o.Final ? 1 : 0).ThenByDescending(o => o.Facility != null ? 1 : 0).ThenByDescending(o => o.Story != null ? 1 : 0)
                .ThenByDescending(o => o.Quest != null ? 1 : 0).ThenBy(o => o.ExpiresDay ?? 999).ToList();
            var offer = pick.Offer != null ? offers.FirstOrDefault(o => o.Id == pick.Offer) : null;
            var region = offer?.Region ?? pick.Region;
            var v = new MapView
            {
                Level = c.Doom,
                Lata = (c.ActiveMission != null ? c.Offers.FirstOrDefault(o => o.Id == c.ActiveMission)?.Region : null) ?? Campaign.HomeRegion,
                Vehicle = Campaign.VehicleOf(c),
                Selected = region,
                Contacts = c.Regions.Count(r => r.Contacted), ContactsMax = Campaign.ContactCapacity(c),
                // Lore, C5: the Sala de radio, not XCOM's centres; the head's help is the screen's own.
                ContactsTip = "Zonas contactadas / máximo (la Sala de radio da más)",
                // Lore, C8: not the glossary's Nido, which names the Marea before level 4.
                NestsTip = Lore.NestTip(level), QuestsTip = Lore.Glossary["Encargo"],
                RumorsTip = $"{Lore.Glossary["Rumor"]} {(c.Listening != null ? "Solo se escucha uno a la vez." : "Se enfría si nadie lo escucha.")}",
                MissionsEmpty = offers.Count == 0 ? "Ninguna a la vista." : null,
            };
            foreach (var r in c.Regions) v.Zones.Add(Zone(c, r, offers, region));
            foreach (var o in offers) v.Missions.Add(MissionRow(c, o, pick.Offer == o.Id));
            v.Wait = WaitDay(c);
            foreach (var r in c.Regions.Where(r => r.Facility != null))
                v.Nests.Add(new MapRowView
                {
                    Id = r.Id, Name = Campaign.Regions[r.Id].Name, Selected = pick.Region == r.Id && offer == null,
                    Chip = r.Contacted ? "Asaltable" : "Sin contacto", ChipTone = r.Contacted ? "bad" : null, ChipTip = r.Contacted ? null : Lore.NestNoContactTip(r.Id, Campaign.ContactCost(c)),
                });
            foreach (var q in c.Quests)
            {
                var def = Quests.QuestDefs[q.Id];
                var o = ShownOffers(c).FirstOrDefault(x => x.Quest == q.Id);
                // Its step waits out a siege (#50), hidden like every other operation.
                var where = o != null ? Campaign.Regions[o.Region].Name : c.Offers.Any(x => x.Quest == q.Id) ? "Tras el asedio" : "Esperando vuestra decisión";
                var whose = q.Owner != null && c.Players.Count > 1 ? $" · de {PlayerName(q.Owner.Value)}" : "";
                // Its id the mission of its step, which a click opens (screen.ts focusOffer); none while it waits, and nothing opens.
                v.Quests.Add(new MapRowView { Id = o?.Id, Name = def.Title, Line = $"Paso {q.Step + 1} de {def.Steps.Count} · {where}{whose}", Chip = o?.ExpiresDay != null ? AheadChip(c, o.ExpiresDay.Value) : null, Selected = o != null && pick.Offer == o.Id });
            }
            foreach (var r in c.Rumors)
            {
                var listening = c.Listening == r.Id;
                var blocked = c.Proposal != null ? "Hay una propuesta pendiente" : null;
                v.Rumors.Add(new RumorRowView
                {
                    // The rumour picked by the rumour itself (UNITY-ESTRATEGIA.md §7: the 0.10.1 compared a zone with a rumour).
                    Id = r.Id, Region = Campaign.Regions[r.Region].Name, Text = $"Se habla de {r.Text}.", Selected = pick.Region == r.Id && offer == null,
                    Listening = listening ? $"Escuchando · {MissionsText(MissionsFor(r.DaysLeft))}" : null,
                    Button = listening ? null : $"{Decide("Escuchar")} · {MissionsText(MissionsFor(r.DaysLeft))}",
                    Enabled = blocked == null, Why = blocked ?? $"Se sabrá {Lore.InMissions(MissionsFor(r.DaysLeft))}",
                    Chip = listening ? null : AheadChip(c, r.ExpiresDay), ChipTone = MissionsFor(r.ExpiresDay - c.Day) <= 1 ? "bad" : "warn",
                });
            }
            if (offer != null) v.Detail = Briefing(c, offer);
            else if (region != null && c.Regions.Any(r => r.Id == region)) v.Detail = RegionInfo(c, region);
            return v;
        }

        /// <summary>A zone's mark (geoscape.ts update): its pin by its state, a Nido first, its operations, a rumour.</summary>
        ZoneMarkView Zone(CampaignState c, RegionState r, List<MissionOffer> offers, string selected)
        {
            var z = new ZoneMarkView { Id = r.Id, Name = Campaign.Regions[r.Id].Name, Contacted = r.Contacted, Flooded = Campaign.UnderMarea(c, r.Id), Selected = r.Id == selected, Nest = r.Facility != null };
            // The siege is at home, not out in the valley: no pin for it.
            foreach (var o in offers.Where(x => x.Region == r.Id && x.Siege != true))
            {
                z.Missions.Add(OfferTone(o));
                z.Kinds.Add(o.Kind);
            }
            var rumor = c.Rumors.FirstOrDefault(x => x.Region == r.Id);
            z.Rumor = rumor != null;
            z.Listening = rumor != null && rumor.Id == c.Listening;
            return z;
        }

        /// <summary>«Esperar un día» (waitDayEl; Ana and Arturo, 22/10 #48): a day at the Bastión, both agreeing.</summary>
        WaitDayView WaitDay(CampaignState c)
        {
            var ready = c.AdvanceReady[Slot];
            var partner = Partner;
            var together = partner != null && partner.Connected && c.Players.Count > 1;
            var asked = together && !ready && c.AdvanceReady[partner.Slot];
            var why = Campaign.ActiveOffer(c)?.Siege == true ? "Viene el asedio: no se puede esperar." : c.Event != null ? "Antes, responded al acontecimiento." : null;
            return new WaitDayView
            {
                Label = ready ? "Esperando…" : "Esperar un día", Ready = ready, Asked = asked, Enabled = why == null || ready, Why = why,
                Note = why == null && ready && together ? $"Falta {partner.Name}" : why == null && asked ? $"{partner.Name} quiere esperar un día" : null,
                Tip = $"Un día sin salir: se come, se curan las heridas y avanzan el Taller y {Lore.DoomSubject(Level)}.",
            };
        }

        /// <summary>A mission's sheet (briefing): what kind, how hard, what it gives, Desplegar or, chosen, the squad and cancelling.</summary>
        MapDetailView Briefing(CampaignState c, MissionOffer o)
        {
            var r = o.Reward ?? new MissionReward();
            var rewards = new List<string>
            {
                r.Credits is int credits && credits != 0 ? $"{credits} suministros" : "",
                r.Recruit != null && !(r.Recruit is bool b && !b) ? "un recluta" : "",
                r.Doom is int doom && doom != 0 ? $"{Lore.DoomSubject(Level)} retrocede {doom}" : "",
                o.Story is int story && story >= 1 && story <= Lore.Story.Count ? Lore.Story[story - 1].Prize ?? "" : "",
            };
            // A better vehicle (#45): what La Lata becomes, and how many it takes then.
            if (r.Vehicle is int vehicle) rewards.Add($"La Lata pasa a ser {(vehicle < Lore.Vehicles.Count ? Lore.Vehicles[vehicle].A : "")} ({(vehicle < Campaign.VehicleSeats.Count ? Campaign.VehicleSeats[vehicle].ToString() : "?")} plazas)");
            var quest = o.Quest != null ? c.Quests.FirstOrDefault(q => q.Id == o.Quest) : null;
            if (quest != null) rewards.Add($"Al final del encargo: {Quests.Fill(Quests.QuestDefs[quest.Id].Prize, quest.Names)}");
            var chosen = c.ActiveMission == o.Id;
            // Lore, C12: «Distrito comercial».
            var biome = o.Map.Kind == MapSourceKinds.Generated && o.Map.Biome != null ? Setup.BiomeNames[o.Map.Biome] : "Distrito comercial";
            var stars = $"<color=#f0503c>{new string('★', o.Difficulty)}</color> {(o.Difficulty < Difficulty.Length ? Difficulty[o.Difficulty] : "")}";
            var signature = Campaign.OfferSignature(c, o);
            var v = new MapDetailView { Title = o.Name, Text = { Campaign.OfferBriefing(c, o) }, Note = string.IsNullOrEmpty(signature) ? null : signature };
            if (o.Siege == true)
            {
                // The siege (docs/FASE12.md §11.5) is fought at home: no region's ground, nothing that expires.
                v.Kicker = Lore.SiegeText.Name;
                v.KickerTone = "final";
                v.Subtitle = o.Night == true ? $"El Bastión · {Lore.NightText.Label}" : "El Bastión";
                v.Rows.Add(new RowView { Key = "Tipo", Value = Lore.SiegeText.Name });
                v.Rows.Add(new RowView { Key = "Dificultad", Value = stars });
            }
            else
            {
                if (o.Story != 1)
                {
                    v.Kicker = o.Final ? "Misión final" : o.Facility != null ? "Nido" : o.Story != null ? $"Misión de historia · Hoja {o.Story}" : o.Quest != null ? $"Encargo · {Quests.QuestDefs[o.Quest].Title}" : "Misión";
                    if (quest != null) v.KickerTip = "Si se pierde o caduca, el encargo se pierde con sus consecuencias";
                    v.KickerTone = o.Final ? "final" : o.Facility != null ? "nest" : o.Story != null ? "story" : o.Quest != null ? "quest" : null;
                }
                v.Subtitle = $"{Campaign.Regions[o.Region].Name} · {biome}{(o.Night == true ? $" · {Lore.NightText.Label}" : "")}{(quest != null ? $" · paso {quest.Step + 1} de {Quests.QuestDefs[quest.Id].Steps.Count}" : "")}";
                // The living hold it (18/10 #47): the dead go for them too.
                if (o.Living == true) v.Warning = Lore.LivingTip;
                v.Rows.Add(new RowView { Key = "Tipo", Value = Setup.MissionNames[o.Kind] });
                v.Rows.Add(new RowView { Key = "Dificultad", Value = stars });
                var reward = string.Join(" · ", rewards.Where(x => x != ""));
                v.Rows.Add(new RowView { Key = "Recompensa", Value = reward != "" ? reward : "La victoria" });
            }
            // Called off here, not from the Escuadra (playtest 19/10 #3). Lore, C10: «Ir a la Escuadra».
            if (chosen)
            {
                v.Buttons.Add(new MapButtonView { Action = "squad", Label = "Ir a la Escuadra", Primary = true });
                if (o.Siege != true) v.Buttons.Add(new MapButtonView { Action = "cancel", Label = "Cancelar misión" });
                return v;
            }
            var blocked = c.ActiveMission != null ? "Ya hay otra misión elegida" : c.Proposal != null ? "Hay una propuesta pendiente" : null;
            v.Buttons.Add(new MapButtonView { Action = "deploy", Label = Decide("Desplegar"), Primary = true, Enabled = blocked == null });
            v.Why = blocked;
            return v;
        }

        /// <summary>A mission's row (screen.ts renderGeoscape's list): its pin, name, where, how long it is there.</summary>
        MissionRowView MissionRow(CampaignState c, MissionOffer o, bool selected)
        {
            var days = o.ExpiresDay != null ? MissionsFor(o.ExpiresDay.Value - c.Day) : (int?)null;
            return new MissionRowView
            {
                Id = o.Id, Tone = o.Final || o.Siege == true ? "#ff5a8a" : OfferTone(o), Kind = o.Kind, Name = o.Name, Selected = selected,
                Where = $"{(o.Siege == true ? "El Bastión" : Campaign.Regions[o.Region].Name)}{(o.Night == true ? $" · {Lore.NightText.Label}" : "")}",
                Tip = o.Siege == true ? $"{Lore.SiegeText.Name} · El Bastión" : $"{(o.Facility != null ? "Nido" : o.Story != null ? $"Historia · {Setup.MissionNames[o.Kind]}" : o.Quest != null ? $"Encargo · {Setup.MissionNames[o.Kind]}" : Setup.MissionNames[o.Kind])} · {Campaign.Regions[o.Region].Name}",
                Chip = o.ExpiresDay != null ? AheadChip(c, o.ExpiresDay.Value) : null, ChipTone = days != null && days <= 1 ? "bad" : null,
                ChipTip = o.ExpiresDay == null ? "No caduca" : $"Caduca {AheadText(c, o.ExpiresDay.Value)}",
            };
        }

        /// <summary>
        /// A zone's sheet (regionInfo): what it is, who is there and what they send, its errand, its Nido, its missions
        /// (Arturo, 26/10: a zone opens its sheet, and a mission is picked from there), Contactar.
        /// </summary>
        MapDetailView RegionInfo(CampaignState c, string id)
        {
            var def = Campaign.Regions[id];
            var r = c.Regions.First(x => x.Id == id);
            var contacted = c.Regions.Count(x => x.Contacted);
            var capacity = Campaign.ContactCapacity(c);
            var cost = Campaign.ContactCost(c);
            var flooded = Campaign.UnderMarea(c, id);
            var level = Level;
            var blocked = First(
                (!r.Contacted && Campaign.ContactBlocker(c, id) != null, Campaign.ContactBlocker(c, id)),
                (contacted >= capacity, "Construid o mejorad la Sala de radio para más contactos"),
                (c.Proposal != null, "Hay una propuesta pendiente"),
                (!Campaign.CanAfford(c, cost), Shortfall(c, cost)));
            var v = new MapDetailView
            {
                Kicker = flooded ? Lore.DoomName(level) : r.Contacted ? "Zona contactada" : "Zona sin contacto", KickerTone = flooded ? "nest" : null,
                Title = def.Name, Text = { Lore.RegionLores[id].Situation },
            };
            if (flooded) v.Lines.Add(new MapLineView { Text = Lore.FloodedZoneNote(level), Tone = "bad" });
            // Lore, C6: each convoy (every 6 missions), not a month; capitalised.
            v.Lines.Add(new MapLineView { Text = $"{Capital(Lore.RegionLores[id].Ally)} · +{Campaign.RegionIncome(c, id)} § en cada convoy", Tip = "Lo que deja en cada convoy, una vez contactada. El convoy llega cada 6 misiones." });
            var errand = RegionQuest(c, id);
            if (errand != null) v.Lines.Add(new MapLineView { Text = errand });
            // Lore, C7: a Nido every 3 missions (FACILITY_DOOM_INTERVAL), not the 0.10.1's 14.
            if (r.Facility != null) v.Lines.Add(new MapLineView { Text = $"Nido: hace avanzar {Lore.DoomSubject(level)} cada {MissionsText(3)}.{(r.Contacted ? "" : $" {Lore.NestNoContactTip(r.Id, cost)}")}", Tone = "bad" });
            foreach (var o in ShownOffers(c).Where(o => o.Region == id && o.Siege != true)) v.Missions.Add(MissionRow(c, o, false));
            if (r.Contacted) return v;
            v.Buttons.Add(new MapButtonView { Action = "contact", Label = $"{Decide("Contactar")} · {Campaign.CostText(cost)}", Primary = true, Enabled = blocked == null });
            v.Why = blocked;
            v.Meter = contacted;
            v.MeterMax = capacity;
            return v;
        }

        /// <summary>Where the zone's own errand stands, as a hint to listen to its rumours (regionQuest); null, none.</summary>
        static string RegionQuest(CampaignState c, string id)
        {
            var quest = Quests.QuestDefs.Values.FirstOrDefault(q => q.Rumor && q.Region == id);
            if (quest == null) return null;
            var done = c.QuestLog.TryGetValue(quest.Id, out var log) ? log : null;
            var contacted = c.Regions.FirstOrDefault(r => r.Id == id)?.Contacted == true;
            var underway = c.Quests.Any(q => q.Id == quest.Id);
            var state = underway ? "en curso"
                : done != null ? ((quest.Outcomes.TryGetValue(done.Outcome, out var o) ? o : quest.Outcomes["failure"]).Tone == "good" ? "cumplido" : "cerrado")
                : contacted ? "escuchad los rumores de la zona para encontrarlo" : "contactad con la zona para que os lo cuenten";
            return $"Encargo de la zona: {(done != null || underway ? $"«{quest.Title}», " : "")}{state}.";
        }
    }
}
