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
                // What of it came into the packs (Pruebas, 0.14.4: «+45» with none of it in, red.log «§ netos 0»); none, no
                // crate, only the line of what stayed there.
                var kept = reward - (run.RewardLost ?? 0);
                if (kept > 0) v.Haul.Add(new HaulView { Kind = GearKinds.Supplies, Icon = "store:parts", Figure = $"+{kept}", Big = true, Tip = StrategyText.Runs.RewardTip(kept) });
                if ((run.RewardLost ?? 0) > 0) v.Lines.Add(new DebriefLineView { Text = StrategyText.Runs.RewardLost(run.RewardLost.Value) });
            }
            // The supplies found in one tile, however many piles they came in (the pass of 16/11: «9» and «10» side by side).
            var supplies = run.Found.Where(g => g.Kind == GearKinds.Supplies).Sum(g => g.Count);
            if (supplies > 0) v.Haul.Add(new HaulView { Kind = GearKinds.Supplies, Icon = GearIconOf(run.Found.First(g => g.Kind == GearKinds.Supplies)), Figure = supplies.ToString(), Tip = Lore.GearName(GearKinds.Supplies, null, supplies) });
            foreach (var g in run.Found.Where(g => g.Kind != GearKinds.Supplies))
                v.Haul.Add(new HaulView { Kind = g.Kind, Type = g.Type, Icon = GearIconOf(g), Figure = g.Count > 1 ? g.Count.ToString() : "", Better = g.Kind == GearKinds.Weapon, Tip = Lore.GearName(g.Kind, g.Type, g.Count) });
            if (v.Haul.Count == 0 && run.Ended != RunEnds.Wiped) v.Lines.Add(new DebriefLineView { Text = StrategyText.Runs.NothingFound });
            foreach (var f in run.Fallen)
            {
                var lost = f.Lost.Select(g => Lore.GearThe(g.Kind, g.Type, g.Count)).ToList();
                // One left outside La Lata hasn't fallen (Red's pass, 18/11): their report says so (ReportStatuses.Lost). A
                // survivor found who never became a soldier (Id «»: Gameplay, 24/11) told apart by their name too.
                var left = report.Squad.Any(e => e.Id == f.Id && (f.Id != "" || e.Name == f.Name) && e.Status == ReportStatuses.Lost);
                var female = c.Soldiers.FirstOrDefault(x => x.Id == f.Id && f.Id != "") is CampaignSoldier fs ? Female(fs) : Campaign.SexOfPerson(f.Name) == "f";
                v.Lines.Add(new DebriefLineView { Text = left ? StrategyText.Runs.LeftLine(f.Name, female, lost) : StrategyText.Runs.FallenLine(f.Name, lost), Bad = true });
            }
            if (run.Joined != null && c.Soldiers.FirstOrDefault(x => x.Id == run.Joined) is CampaignSoldier rescued)
                v.Lines.Add(new DebriefLineView { Text = StrategyText.Runs.JoinedLine(rescued.Name) });
            // Got out with no bed for them (Gameplay's NoBed, 24/11): they don't stay; what they carried went into the packs,
            // and what didn't fit is lost.
            if (run.NoBed != null)
                v.Lines.Add(new DebriefLineView { Text = StrategyText.Runs.NoBedLine(run.NoBed.Name, run.NoBed.Gave.Count > 0, run.NoBed.Lost.Select(g => Lore.GearThe(g.Kind, g.Type, g.Count)).ToList()), Bad = run.NoBed.Lost.Count > 0 });
            // (One a line: whoever came in late has them all from the report, ReportRecruits.)
            if (Joined != null) foreach (var line in Joined.Split('\n')) v.Lines.Add(new DebriefLineView { Text = line });
            // What came home (cuadro 165), the expedition over in this fight: the trunk's and the Huerto's lines, as the log has
            // them (HomeLines); the trunk's with things lost, red.
            if (BackHome != null)
                foreach (var line in BackHome.Split('\n'))
                    v.Lines.Add(new DebriefLineView { Text = line, Bad = Enumerable.Range(1, 32).Any(k => line == RunTexts.TrunkHome(k)) });
            foreach (var e in ReportOrder(c, report))
            {
                var card = ReportCard(c, report, e);
                // One who never became a soldier (the survivor left outside La Lata): their bust as the fight drew it (R23 B8).
                if (e.Id == "")
                {
                    card.Bust = "persona:" + e.Name;
                    card.Look = SurvivorLook(c, run, e.Name);
                    // (The fight's bust was by their unit's id: the Rescate's «vip», the loose one's «survivor».)
                    card.BustSeed = run.NodeKind == NodeKinds.Rescue ? "vip" : "survivor";
                }
                // In a run, the health each has left to the next stage, not days of wounds.
                if (e.Status != "dead" && run.Hp.TryGetValue(e.Id, out var hp) && c.Soldiers.FirstOrDefault(x => x.Id == e.Id) is CampaignSoldier s)
                    card.Lines[0] = StrategyText.Stash.Health(hp, Runs.MaxHp(s)).ToUpperInvariant();
                v.Cards.Add(card);
            }
            return v;
        }

        /// <summary>
        /// The look of a run's survivor named `name`, as their fight drew them (Runs.SurvivorOf, from the node's seed), while
        /// the node is on the expedition's map; null when it isn't (the expedition over) or the name isn't theirs.
        /// </summary>
        static Appearance SurvivorLook(CampaignState c, RunReport run, string name)
        {
            var node = c.Run?.Nodes.FirstOrDefault(n => n.Id == run.Node);
            if (node == null) return null;
            var member = Runs.SurvivorOf(c, node);
            return member.Name == name ? member.Appearance : null;
        }
    }
}
