#nullable enable
// campaign.ts nextSteps: what a player has pending in the base, for the "Ahora" block and the nav counters. Texts fixed as
// text-fixes.ts has them (Gameplay and Lore, 24/10): La Lata «sale», it doesn't take off; a piece's name; the time in
// missions.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ------------------------------------------------------------- next steps

        /// <summary>
        /// What `slot` has pending, most important first: the first item is "what to do
        /// now". Shared matters (operations, building, rumours) and personal ones
        /// (promotions, equipment) mixed by urgency.
        /// `partnerName`: who the other player is, for lines that speak of them (Lore, 18/10: «compañero» doesn't fit
        /// Ana); the lines don't depend on whether it is a man or a woman.
        /// </summary>
        public static List<NextStep> NextSteps(CampaignState c, int slot, string partnerName = "El otro jugador")
        {
            if (c.Outcome != null) return new List<NextStep>();
            var steps = new List<NextStep>();
            var partner = slot == 0 ? 1 : 0;
            var shared = c.Players.Count > 1;
            NextStep Step(string view, string label, string tone, int? count = null, NextStepGo? go = null) => new NextStep { View = view, Label = label, Tone = tone, Count = count, Go = go };

            if (c.Proposal != null && c.Proposal.By != slot) steps.Add(Step(BaseViews.Hub, $"{partnerName} propone {c.Proposal.Text}", NextStepTones.Urgent));
            var @event = EventDef(c);
            if (@event != null) steps.Add(Step(BaseViews.Hub, $"Decidid: {@event.Title}", NextStepTones.Urgent));

            var mission = ActiveOffer(c);
            var mine = c.Soldiers.Where((s) => s.Owner == slot && s.Alive).ToList();

            // Short of soldiers for a full share of the squad (after a bad mission): recruiting comes
            // before choosing an operation, or the squad flies half empty.
            var ready = mine.Count(SoldierReady);
            // With no bed free, the answer is a barracón first.
            var fewReady = ready == 0 ? "Ningún soldado listo" : $"Solo {ready} {(ready == 1 ? "soldado listo" : "soldados listos")}";
            var needed = ready < SquadLimit(c, c.Players) && mine.Count < SoldierCap(c);
            var @short = needed && BedsFree(c) > 0 && CanAfford(c, RecruitCost(c))
                ? Step(BaseViews.Barracks, $"{fewReady}: recluta ({RecruitCost(c)} §)", NextStepTones.Info)
                : needed && BedsFree(c) <= 0 && c.Base.Works.Count < WorkCrews(c) && CanAfford(c, Facilities[FacilityIds.Quarters].Cost)
                    // «construye un barracón» (text-fixes.ts, Lore 24/10): a piece's name.
                    ? Step(BaseViews.Facilities, $"{fewReady} y sin camas: construye un Barracón", NextStepTones.Info)
                    : null;
            if (@short != null && mission == null) steps.Add(@short);

            if (mission?.Siege == true)
            {
                // The dead at the base: straight to the squad, by La Lata, where everyone healthy is already on it.
                var partnerReady = shared && c.LaunchReady[partner] && !c.LaunchReady[slot];
                steps.Add(Step(BaseViews.Squad, partnerReady ? $"{partnerName} ya ha dado el sí: {mission.Name}" : $"{Lore.BaseSiegeText.Here} Defended el Bastión", NextStepTones.Urgent));
            }
            else if (mission != null)
            {
                var partnerReady = shared && c.LaunchReady[partner] && !c.LaunchReady[slot];
                // «despegar», «despega» (text-fixes.ts, Lore 24/10): La Lata doesn't take off.
                steps.Add(Step(BaseViews.Squad, partnerReady ? $"{partnerName} ya quiere salir: {mission.Name}" : $"Revisa la escuadra y sal: {mission.Name}", partnerReady ? NextStepTones.Urgent : NextStepTones.Info, null, new NextStepGo { Offer = mission.Id }));
                if (@short != null) steps.Add(@short);
            }
            else if (c.Offers.Count > 0)
            {
                // Expiring soon first; then story chapters, which move the campaign on.
                double Left(MissionOffer o) => o.ExpiresDay == null ? double.PositiveInfinity : o.ExpiresDay.Value - c.Day;
                var sorted = c.Offers.ToList();
                Js.Sort(sorted, (a, b) =>
                {
                    // `left(a) - left(b) || …`: 0 and NaN (Infinity less Infinity) both go on to the story.
                    var d = Left(a) - Left(b);
                    if (d != 0 && !double.IsNaN(d)) return d > 0 ? 1 : -1;
                    return ((b.Story ?? 0) != 0 ? 1 : 0) - ((a.Story ?? 0) != 0 ? 1 : 0);
                });
                var first = sorted[0];
                var left = Left(first);
                var soon = left <= 2;
                // «caduca en N días» (text-fixes.ts, Lore 24/10): in missions, as the Mapa says it.
                steps.Add(Step(BaseViews.Geoscape, soon ? $"{first.Name} caduca {(left <= 0 ? "hoy" : Lore.InMissions((int)left))}" : "Elige una misión", soon ? NextStepTones.Urgent : NextStepTones.Info, c.Offers.Count, new NextStepGo { Offer = first.Id }));
            }

            // The base (docs/FASE12.md §11): the siege close and the base not ready; what it broke or spent; a Sala de radio to
            // hear it coming. Right after the operations: it is what the next siege is fought with (18/10 test).
            var pieces = c.Bastion?.Pieces ?? new List<BasePiece>();
            var broken = pieces.FirstOrDefault((p) => p.Hp < Base.Pieces[p.Piece].Hp && CanAfford(c, Base.RepairCost(p).Supplies));
            var spent = pieces.FirstOrDefault((p) => p.Hp > 0 && Base.RearmCost(p) > 0 && (c.Parts ?? 0) >= Base.RearmCost(p));
            if (c.Bastion != null && c.Siege.In <= 1 && ActiveOffer(c)?.Siege != true) steps.Add(Step(BaseViews.Facilities, "Prepara la base: el asedio está cerca", NextStepTones.Urgent));
            if (broken != null) steps.Add(Step(BaseViews.Facilities, $"Repara {Lore.PieceTexts.All[broken.Piece].The}", NextStepTones.Info, null, new NextStepGo { Piece = broken.Id }));
            if (spent != null) steps.Add(Step(BaseViews.Facilities, $"Repón {Lore.PieceTexts.All[spent.Piece].The}", NextStepTones.Info, null, new NextStepGo { Piece = spent.Id }));
            if (broken == null && spent == null && c.Bastion != null && !c.Bastion.Pieces.Any((p) => p.Piece == PieceIds.Radio) && CanAfford(c, Base.Pieces[PieceIds.Radio].Cost.Supplies))
            {
                steps.Add(Step(BaseViews.Facilities, $"Construye {Lore.PieceTexts.All[PieceIds.Radio].A}", NextStepTones.Info));
            }

            var promotions = mine.Where((s) => s.Alive && CanSpendPoints(c, s)).ToList();
            if (promotions.Count > 0)
            {
                steps.Add(Step(BaseViews.Barracks, promotions.Count == 1 ? $"Puntos por gastar: {promotions[0].Name}" : "Puntos por gastar", NextStepTones.Info, promotions.Count, new NextStepGo { Soldier = promotions[0].Id }));
            }

            if (mission == null && shared && c.AdvanceReady[partner] && !c.AdvanceReady[slot])
            {
                steps.Add(Step(BaseViews.Geoscape, $"{partnerName} quiere esperar a que se curen", NextStepTones.Urgent));
            }
            if (MustWait(c) && !c.AdvanceReady[slot]) steps.Add(Step(BaseViews.Barracks, "Nadie puede salir: esperad a que se curen", NextStepTones.Urgent));

            // The Marea (docs/FASE5.md): the zones it covers send nothing and their missions are harder.
            var flooded = FloodedZones(c);
            if (flooded.Count > 0) steps.Add(Step(BaseViews.Geoscape, Lore.FloodedListLabel(Progression.BastionLevel(c), flooded.Select((id) => Regions[id].Name).ToList()), NextStepTones.Info));

            // Food (docs/FASE4.md): hunger, or the pantry running low.
            // Counted in missions now, five days each (18/10 #37, Lore's words).
            var missions = (c.Food ?? 0) / Math.Max(1, DailyRations(c) * DaysPerMission);
            if (c.Hungry == true) steps.Add(Step(BaseViews.Facilities, "Pasáis hambre: −10 de voluntad y heridas lentas. Buscad raciones o un Huerto", NextStepTones.Urgent));
            else if (missions < 2) steps.Add(Step(BaseViews.Facilities, Lore.RationsLowLine(missions), missions < 1 ? NextStepTones.Urgent : NextStepTones.Info));

            // The Bastion one level up, once it can be bought (18/10 #40).
            var up = NextLevel(c);
            if (up != null && LevelUpBlocker(c) == null) steps.Add(Step(BaseViews.Hub, $"Sube el Bastión a {Lore.BastionLevelThe(up.Level)} ({up.Cost} §)", NextStepTones.Info));
            // The Taller has something within reach to make.
            var craft = Progression.CraftOrder.FirstOrDefault((id) => CraftBlocker(c, id) == null);
            if (craft != null) steps.Add(Step(BaseViews.Engineering, $"El Taller puede hacer {Progression.Crafts[craft].The}", NextStepTones.Info));

            // The bitten (docs/FASE3.md): the partner's treatment to confirm, and every fever with its deadline.
            var treating = c.Treatment != null ? c.Soldiers.FirstOrDefault((s) => s.Id == c.Treatment.Soldier) : null;
            if (treating != null && shared && c.Treatment!.Ready[partner] && !c.Treatment.Ready[slot])
            {
                steps.Add(Step(BaseViews.Barracks, $"{partnerName} quiere tratar a {treating.Name} ({TreatCost(c)} ✚)", NextStepTones.Urgent, null, new NextStepGo { Soldier = treating.Id }));
            }
            var bitten = c.Soldiers.Where((x) => x.Alive && x.Infection != null).ToList();
            Js.Sort(bitten, (a, b) => TurnDay(c, a)!.Value - TurnDay(c, b)!.Value);
            foreach (var s in bitten)
            {
                var left = TurnDay(c, s)!.Value - c.Day;
                // «mañana», «en N días» (text-fixes.ts, Lore 24/10): in missions, as the soldier's sheet says it.
                steps.Add(Step(BaseViews.Barracks, $"{s.Name} se convierte {(left <= 1 ? "tras la próxima misión" : Lore.InMissions(left))}", NextStepTones.Urgent, null, new NextStepGo { Soldier = s.Id }));
            }

            if (c.Listening == null && c.Rumors.Count > 0)
            {
                var rumors = c.Rumors.ToList();
                Js.Sort(rumors, (a, b) => a.ExpiresDay - b.ExpiresDay);
                var rumor = rumors[0];
                steps.Add(Step(BaseViews.Geoscape, "Escucha un rumor", NextStepTones.Info, c.Rumors.Count, new NextStepGo { Region = rumor.Region, Rumor = rumor.Id }));
            }

            // Before a mission: accessories on the shelf while someone in your squad has an empty place at the belt (both
            // places of the Loadout take any accessory, docs/FASE9.md §4).
            if (mission != null)
            {
                var free = Content.Items.Values.Where((i) => i.Slot == ItemSlots.Utility).Sum((i) => Math.Max(0, ItemsFree(c, i.Id)));
                var going = mine.Where((s) => c.Squad.Contains(s.Id)).ToList();
                var fillable = Math.Min(free, going.Sum((s) => (string.IsNullOrEmpty(s.Loadout.Utility) ? 1 : 0) + (string.IsNullOrEmpty(s.Loadout.Ammo) ? 1 : 0)));
                var first = free > 0 ? going.FirstOrDefault((s) => string.IsNullOrEmpty(s.Loadout.Utility) || string.IsNullOrEmpty(s.Loadout.Ammo)) : null;
                if (fillable != 0 && first != null) steps.Add(Step(BaseViews.Barracks, "Equipa a tu escuadra con lo del almacén", NextStepTones.Info, fillable, new NextStepGo { Soldier = first.Id }));
            }

            if (c.Regions.Count((r) => r.Contacted) < ContactCapacity(c) && CanAfford(c, ContactCost(c))) steps.Add(Step(BaseViews.Geoscape, "Contacta una zona", NextStepTones.Info));

            // Urgent first, keeping the order above within each tone.
            return steps.Where((x) => x.Tone == NextStepTones.Urgent).Concat(steps.Where((x) => x.Tone == NextStepTones.Info)).ToList();
        }
    }
}
