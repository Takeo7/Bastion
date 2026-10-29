#nullable enable
// campaign.ts migrateCampaign: a campaign saved by an earlier version of the current progression brought up to now.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ---------------------------------------------------------------- migration

        /// <summary>The Bastion experience each level took before version 21 (index 0 is level 1), for migrateCampaign.</summary>
        private static readonly IReadOnlyList<int> OldLevelXp = new[] { 0, 12, 30, 55, 85, 120, 160, 205 };

        /// <summary>
        /// Brings a campaign saved by an earlier version of the current progression
        /// up to the current shape. Campaigns from before Fase 10 aren't carried over
        /// (campaignOutdated, docs/FASE10.md): the host keeps them aside instead.
        /// </summary>
        /// <remarks>
        /// TypeScript works on a copy (JSON.parse(JSON.stringify(old))); here the campaign given is changed where it is and
        /// given back, as whoever calls it keeps only what it gives back (the room's save, read just for this).
        /// </remarks>
        public static CampaignState MigrateCampaign(CampaignState old)
        {
            if (CampaignOutdated(old.Version)) throw new InvalidOperationException($"Campaña de la versión {old.Version}: de antes de la 0.7, no se convierte.");
            var c = old;
            var version = old.Version;
            c.Version = CampaignVersion;
            if (version < 21)
            {
                // The level is bought from now on (#40): it starts where the experience had it, with the levels' experience then.
                c.Level ??= Math.Max(1, OldLevelXp.Count((xp) => c.Xp >= xp));
                // Operations don't expire (#39).
                foreach (var o in c.Offers) o.ExpiresDay = null;
            }
            if (version < 22)
            {
                // La Lata grows from a 4×4 (#45): a campaign under way keeps the six seats it had, the furgoneta's.
                c.Vehicle ??= 1;
                // The Bastion's lot (#40): what stood on it comes down, all its cost back.
                c.Bastion.Level = Progression.BastionLevel(c);
                foreach (var p in c.Bastion.Pieces.ToList())
                {
                    // (At the size it was built then: docs/MAPAS.md phase 6 made the pieces bigger since.)
                    var size = Base.OldPieceSizes.TryGetValue(p.Piece, out var was) ? was : null;
                    if (p.Piece == PieceIds.Caravan || !Base.PieceCells(p.Piece, p.X, p.Y, p.Rot, size).Any((t) => Base.OnBastionLot(c.Bastion, t.X, t.Y))) continue;
                    var def = Base.Pieces[p.Piece];
                    c.Bastion.Pieces = c.Bastion.Pieces.Where((q) => q != p).ToList();
                    c.Credits += def.Cost.Supplies;
                    c.Parts = (c.Parts ?? 0) + (def.Cost.Parts ?? 0);
                    Log(c, $"Se desmonta {Lore.PieceTexts.All[p.Piece].The} para dejar sitio al Bastión: +{def.Cost.Supplies} §.");
                }
            }
            if (version < 23)
            {
                // The base grows to 56 (docs/MAPAS.md phase 6): what was built stays in the middle, at its size; what stood on the
                // Bastion's bigger lot comes down, all its cost back; the posts move with the ground.
                var was = c.Bastion.Width;
                foreach (var p in Base.GrowBase(c.Bastion, Base.BaseMap, new Rng(unchecked((uint)(c.Day * 7919 + c.Bastion.NextPiece)))))
                {
                    var def = Base.Pieces[p.Piece];
                    c.Credits += def.Cost.Supplies;
                    c.Parts = (c.Parts ?? 0) + (def.Cost.Parts ?? 0);
                    Log(c, $"Se desmonta {Lore.PieceTexts.All[p.Piece].The} para dejar sitio al Bastión: +{def.Cost.Supplies} §.");
                }
                var o = (c.Bastion.Width - was) / 2;
                if (o > 0 && c.SiegePosts != null)
                {
                    foreach (var p in c.SiegePosts.Values)
                    {
                        p.X += o;
                        p.Y += o;
                    }
                }
            }
            if (version < 24)
            {
                // Sidekick figures (docs/SIDEKICK.md): a soldier nobody customized, all helmet and normal build as everyone,
                // gets a head and a build of its own.
                foreach (var s in c.Soldiers)
                {
                    var sex = s.Appearance.Sex;
                    if (SameLook(s.Appearance, Appearances.DefaultAppearance(s.Owner, null, sex))) s.Appearance = Appearances.DefaultAppearance(s.Owner, null, sex, s.Id);
                }
            }
            if (version < 25)
            {
                // A mission is one day now, not five (Arturo, 21/10, #34): what was still to come, in the days of before, comes
                // in missions, one for every five of them, rounded up; what is past, the same way back.
                int Ahead(int day) => day > c.Day ? c.Day + OldDaysToNow(day - c.Day) : day;
                int Behind(int day) => day < c.Day ? c.Day - (int)Math.Ceiling((c.Day - day) / (double)OldDaysPerMission) : day;
                c.NextMissionDay = Ahead(c.NextMissionDay);
                c.NextDoomDay = Ahead(c.NextDoomDay);
                c.NextFacilityDay = Ahead(c.NextFacilityDay);
                c.NextSupplyDay = Ahead(c.NextSupplyDay);
                c.NextEventDay = Ahead(c.NextEventDay);
                c.NextRumorDay = Ahead(c.NextRumorDay);
                if (c.NextFoodDay != null) c.NextFoodDay = Ahead(c.NextFoodDay.Value);
                if (c.NextMedicineDay != null) c.NextMedicineDay = Ahead(c.NextMedicineDay.Value);
                if (c.Story.NextDay != null) c.Story.NextDay = Ahead(c.Story.NextDay.Value);
                foreach (var r in c.Regions) if (r.Facility != null) r.Facility.NextDoomDay = Ahead(r.Facility.NextDoomDay);
                foreach (var offer in c.Offers) if (offer.ExpiresDay != null) offer.ExpiresDay = Ahead(offer.ExpiresDay.Value);
                foreach (var r in c.Rumors)
                {
                    r.ExpiresDay = Ahead(r.ExpiresDay);
                    r.DaysLeft = OldDaysToNow(r.DaysLeft);
                }
                foreach (var w in c.Base.Works) w.DaysLeft = OldDaysToNow(w.DaysLeft);
                foreach (var s in c.Soldiers)
                {
                    if (s.WoundedDays > 0) s.WoundedDays = OldDaysToNow(s.WoundedDays);
                    if (s.Infection != null) s.Infection.Since = Behind(s.Infection.Since);
                }
                foreach (var q in c.QuestLog.Values) if (q != null) q.Day = Behind(q.Day);
            }
            if (version < 26)
            {
                // One track, to the south (Ana's playtest 28/10): the other three grow over, as the wood either side of them.
                Base.PlantOldTracks(c.Bastion, c.SiegePosts?.Values ?? Enumerable.Empty<Vec2>());
            }

            // `??=` in TypeScript: a soldier read without them (C# reads them as empty) gets them.
            foreach (var s in c.Soldiers)
            {
                if (s.Skills.Count == 0) s.Skills = Bars.NoSkills();
                if (s.Growth.Count == 0) s.Growth = Bars.NoGrowth();
            }
            c.LaunchReady = new bool[2];
            return c;
        }
    }
}
