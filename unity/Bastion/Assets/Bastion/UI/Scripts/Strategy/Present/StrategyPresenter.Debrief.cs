using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The report of a stage of an expedition (H3; MissionReport.Run): how it ended (the stage cleared, the chapter, back
    /// to the base, the squad lost), what came back in the packs as tiles, the reward and what of it didn't fit, the fallen
    /// with what was lost with them, and a card for each with the health left (the bars and what was learnt as ever).
    /// </summary>
    public sealed partial class StrategyPresenter
    {
        DebriefView RunDebrief(CampaignState c, MissionReport report)
        {
            var run = report.Run;
            var v = new DebriefView
            {
                Victory = run.Ended != RunEnds.Wiped,
                Result = StrategyText.Runs.Ended(run.Ended, report.Victory, run.NodeKind),
                Mission = report.MissionName,
                Continue = "Continuar",
            };
            // The reward first, as the crate with its figure; then what was found, each with Arte's picture.
            if (run.Reward is int reward && reward > 0)
            {
                v.Haul.Add(new HaulView { Kind = GearKinds.Supplies, Icon = "store:parts", Figure = $"+{reward}", Big = true, Tip = StrategyText.Runs.RewardTip(reward) });
                if ((run.RewardLost ?? 0) > 0) v.Lines.Add(new DebriefLineView { Text = StrategyText.Runs.RewardLost(run.RewardLost.Value) });
            }
            // The supplies found in one tile, however many piles they came in (the pass of 16/11: «9» and «10» side by side).
            var supplies = run.Found.Where(g => g.Kind == GearKinds.Supplies).Sum(g => g.Count);
            if (supplies > 0) v.Haul.Add(new HaulView { Kind = GearKinds.Supplies, Icon = GearIconOf(run.Found.First(g => g.Kind == GearKinds.Supplies)), Figure = supplies.ToString(), Tip = Lore.GearName(GearKinds.Supplies, null, supplies) });
            foreach (var g in run.Found.Where(g => g.Kind != GearKinds.Supplies))
                v.Haul.Add(new HaulView { Kind = g.Kind, Type = g.Type, Icon = GearIconOf(g), Figure = g.Count > 1 ? g.Count.ToString() : "", Better = g.Kind == GearKinds.Weapon, Tip = Lore.GearName(g.Kind, g.Type, g.Count) });
            if (v.Haul.Count == 0 && run.Ended != RunEnds.Wiped) v.Lines.Add(new DebriefLineView { Text = StrategyText.Runs.NothingFound });
            foreach (var f in run.Fallen)
                v.Lines.Add(new DebriefLineView { Text = StrategyText.Runs.FallenLine(f.Name, f.Lost.Select(g => Lore.GearThe(g.Kind, g.Type, g.Count)).ToList()), Bad = true });
            if (run.Joined != null && c.Soldiers.FirstOrDefault(x => x.Id == run.Joined) is CampaignSoldier rescued)
                v.Lines.Add(new DebriefLineView { Text = StrategyText.Runs.JoinedLine(rescued.Name) });
            // (One a line: whoever came in late has them all from the report, ReportRecruits.)
            if (Joined != null) foreach (var line in Joined.Split('\n')) v.Lines.Add(new DebriefLineView { Text = line });
            foreach (var e in ReportOrder(c, report))
            {
                var card = ReportCard(c, report, e);
                // In a run, the health each has left to the next stage, not days of wounds.
                if (e.Status != "dead" && run.Hp.TryGetValue(e.Id, out var hp) && c.Soldiers.FirstOrDefault(x => x.Id == e.Id) is CampaignSoldier s)
                    card.Lines[0] = StrategyText.Stash.Health(hp, Runs.MaxHp(s)).ToUpperInvariant();
                v.Cards.Add(card);
            }
            return v;
        }
    }
}
