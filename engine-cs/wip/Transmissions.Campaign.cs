// transmissions.ts recapLines, which needs campaign.ts nextSteps in C# (Lore's, 24/10). Off until it exists, so this
// folder still builds; then drop the #if and move this into package/Runtime/Transmissions.cs.
//
// What it expects of the campaign's port:
//   List<NextStep> Campaign.NextSteps(CampaignState c, int slot, string partnerName = "El otro jugador")   nextSteps
// Any list type will do: recapLines reads only the first step's Label.
#if CAMPAIGN_NEXT_STEPS
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Transmissions
    {
        /// <summary>Where the campaign stands, for `slot`: the day, the Bastion, the Marea, their soldiers and the first thing to do.</summary>
        public static List<SceneLine> RecapLines(CampaignState c, int slot)
        {
            var level = Level(c);
            var story = c.Story.Done != 0 ? $"Llevamos {c.Story.Done} de {Lore.Story.Count} hojas del diario." : "El diario está en la primera hoja.";
            var mine = c.Soldiers.Where(s => s.Owner == slot && s.Alive).Select(s => s.Name).ToList();
            var names = mine.Count > 1 ? $"{string.Join(", ", mine.Take(mine.Count - 1))} y {mine[mine.Count - 1]}" : mine.Count == 1 ? mine[0] : "nadie todavía";
            var first = Campaign.NextSteps(c, slot).FirstOrDefault();
            var lines = new List<SceneLine>
            {
                new SceneLine { Who = SpeakerIds.Morse, Text = $"{{yo}}: {Outings(c.MissionsFlown ?? 0)}. El Bastión es {Lore.BastionLevelA(level)}." },
                new SceneLine { Who = SpeakerIds.Morse, Text = $"{Lore.DoomStatus(level, c.Doom)}. {story}" },
                new SceneLine { Who = SpeakerIds.Teo, Text = $"A tu mando: {names}." },
            };
            if (first != null) lines.Add(new SceneLine { Who = SpeakerIds.Morse, Text = $"Lo más urgente: {first.Label.Substring(0, 1).ToLowerInvariant()}{first.Label.Substring(1)}." });
            return lines;
        }
    }
}
#endif
