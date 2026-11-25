#nullable enable
// The commands of a run campaign (.dev-snapshots/gameplay/runs/H0-DISENO.md §5; new in C#): Campaign.ExecuteCampaignCommand
// hands them all here (Campaign.IsRuns). The gear's and the stop's go at once, each player with their own; the
// upgrades and the run's way go to a vote with the partner here (RunAction), alone at once; a vote on one of them is
// answered here. Of the commands of before, only unlock, customize, vote and withdraw stay; the rest are refused.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Runs
    {
        public static CampaignResult Execute(CampaignState c, CampaignCommand cmd, CampaignContext ctx)
        {
            // As Campaign's: «La campaña ha terminado.».
            if (c.Outcome != null) return CampaignResult.Failure("La campaña ha terminado.");
            var slot = ctx.Slot;
            var connected = ctx.Connected;
            switch (cmd)
            {
                case MoveGearCommand m:
                    return Done(Gears.MoveRefusal(c, slot, connected, m), () => Gears.Move(c, slot, connected, m));
                case SplitGearCommand m:
                    return Done(Gears.SplitRefusal(c, slot, connected, m), () => Gears.SplitGear(c, slot, connected, m));
                case DropGearCommand m:
                    return Done(Gears.DropRefusal(c, slot, connected, m), () => Gears.Drop(c, m));
                case MountCommand m:
                    return Done(Gears.MountRefusal(c, slot, connected, m), () => Gears.Mount(c, m));
                case UnmountCommand m:
                    return Done(Gears.UnmountRefusal(c, slot, connected, m), () => Gears.Unmount(c, m));
                case UseGearCommand m:
                    return Done(Gears.UseGearRefusal(c, slot, connected, m), () => Gears.Use(c, m));
                case BuyWareCommand m:
                    return Done(Gears.BuyWareRefusal(c, slot, connected, m), () => Gears.BuyWare(c, slot, connected, m));
                case SellGearCommand m:
                    return Done(Gears.SellRefusal(c, slot, connected, m), () => Gears.Sell(c, slot, connected, m));
                case WorkshopCommand m:
                    return Done(Gears.WorkshopRefusal(c, m), () => Gears.Workshop(c, m));
                case ScrapGearCommand m:
                    return Done(Gears.ScrapRefusal(c, m), () => Gears.Scrap(c, m));
                case SquadCommand m:
                    return Done(SquadRefusal(c, slot, connected, m), () => Squad(c, m));
                case LeaveStopCommand m:
                    return Done(m.Ready ? LeaveStopRefusal(c) : c.Run?.Stop != null && c.Run.Phase == RunPhases.Stop ? null : RunTexts.NotAtStop, () => LeaveStop(c, slot, connected, m.Ready));
                case UpgradeCommand m:
                    return Propose(c, new BuyUpgradeAction { Upgrade = m.Upgrade }, ctx);
                case StartRunCommand _:
                    return Propose(c, new StartRunAction(), ctx);
                case ChooseNodeCommand m:
                    return Propose(c, new ChooseNodeAction { Node = m.Node }, ctx);
                case EndRunCommand _:
                    return Propose(c, new EndRunAction(), ctx);
                case AnswerStopCommand m:
                    return Propose(c, new AnswerStopAction { Node = c.Run?.At ?? "", Option = m.Option }, ctx);
                case VoteCommand m when c.Proposal?.Action is RunAction:
                    return Vote(c, m, ctx);
                case UnlockCommand m:
                {
                    // Between stages, at a stop too; not with a fight going on (G15, Producto 18/11): the fighter was made
                    // at its launch, and what it carries comes back over the kit when it ends (KitBack).
                    if (c.Run?.Phase == RunPhases.Fight) return CampaignResult.Failure(RunTexts.LearnInFight);
                    var result = Campaign.RunsBaseCommand(c, cmd, ctx);
                    // «Todo cabe» (H3): a row more in the pack at once.
                    var s = c.Soldiers.FirstOrDefault((x) => x.Id == m.Soldier);
                    if (result.Ok && m.Cell == CareCell && s?.Kit != null) s.Kit.Pack.H += 1;
                    return result;
                }
                case VoteCommand _:
                case WithdrawCommand _:
                case CustomizeCommand _:
                    return Campaign.RunsBaseCommand(c, cmd, ctx);
                default:
                    // As Campaign's: the base of before's orders (building, missions, the Armería…) don't go in a run campaign.
                    return CampaignResult.Failure("Esa orden no existe.");
            }
        }

        private static CampaignResult Done(string? refusal, Action apply)
        {
            if (refusal != null) return CampaignResult.Failure(refusal);
            apply();
            return CampaignResult.Success();
        }

        // ------------------------------------------------------------------ votes

        /// <summary>Why a joint decision of the runs can't be taken now (each command's refusal), or null.</summary>
        public static string? Refusal(CampaignState c, RunAction action, IReadOnlyList<int> connected)
        {
            switch (action)
            {
                case BuyUpgradeAction a:
                    return UpgradeRefusal(c, a.Upgrade);
                case StartRunAction _:
                    return StartRunRefusal(c, connected);
                case ChooseNodeAction a:
                    return ChooseNodeRefusal(c, a.Node);
                case EndRunAction _:
                    return EndRunRefusal(c);
                case AnswerStopAction a:
                    return AnswerStopRefusal(c, a.Option, a.Node);
                default:
                    return RunTexts.NoOption;
            }
        }

        /// <summary>What a joint decision does, once taken: a node with a fight asks the room to launch it.</summary>
        private static CampaignResult Apply(CampaignState c, RunAction action, CampaignContext ctx)
        {
            switch (action)
            {
                case BuyUpgradeAction a:
                    Upgrade(c, a.Upgrade);
                    break;
                case StartRunAction _:
                    StartRun(c, ctx.Rng);
                    // One here with none of theirs in the squad (N8: left with nobody alive) commands half of it, as on joining.
                    foreach (var p in ctx.Connected) HandOver(c, p, ctx.Connected);
                    break;
                case ChooseNodeAction a:
                    return CampaignResult.Success(ChooseNode(c, a.Node, ctx.Rng));
                case EndRunAction _:
                    EndRun(c, ctx.Rng);
                    break;
                case AnswerStopAction a:
                    AnswerStop(c, a.Option, ctx.Rng);
                    break;
            }
            return CampaignResult.Success();
        }

        /// <summary>The proposal's line, as the partner reads it.</summary>
        public static string ProposalText(CampaignState c, RunAction action)
        {
            switch (action)
            {
                case BuyUpgradeAction a:
                    return RunTexts.UpgradeText(a.Upgrade, NextPrice(c, a.Upgrade) ?? 0);
                case StartRunAction _:
                    return RunTexts.StartRunText(c.Chapter ?? 1);
                case ChooseNodeAction a:
                {
                    var n = c.Run != null ? RunMap.Node(c.Run, a.Node) : null;
                    return n != null ? RunTexts.ChooseNodeText(n) : "";
                }
                case EndRunAction _:
                    return RunTexts.EndRunText;
                case AnswerStopAction a:
                    return c.Run?.Stop != null ? RunTexts.AnswerText(c.Run.Stop, a.Option) : "";
                default:
                    return "";
            }
        }

        /// <summary>The § a proposal would spend, for the counter meanwhile (as Campaign.ProposalCost).</summary>
        public static int ProposalCost(CampaignState c, RunAction action)
        {
            return action is BuyUpgradeAction a ? NextPrice(c, a.Upgrade) ?? 0 : 0;
        }

        /// <summary>Alone, at once; with the partner here, proposed (as Campaign's Propose).</summary>
        private static CampaignResult Propose(CampaignState c, RunAction action, CampaignContext ctx)
        {
            var error = Refusal(c, action, ctx.Connected);
            if (error != null) return CampaignResult.Failure(error);
            var partnerHere = ctx.Connected.Any((s) => s != ctx.Slot) && c.Players.Count > 1;
            if (!partnerHere) return Apply(c, action, ctx);
            if (c.Proposal != null) return CampaignResult.Failure(RunTexts.Pending);
            c.Proposal = new Proposal { By = ctx.Slot, Action = action, Text = ProposalText(c, action) };
            return CampaignResult.Success();
        }

        /// <summary>The answer to a proposal of the runs, as Campaign's vote: what no longer goes through is dropped and said so.</summary>
        private static CampaignResult Vote(CampaignState c, VoteCommand m, CampaignContext ctx)
        {
            var p = c.Proposal!;
            // The lines as Campaign's vote says them.
            if (p.By == ctx.Slot) return CampaignResult.Failure("No puedes votar tu propia propuesta.");
            c.Proposal = null;
            if (!m.Accept)
            {
                Campaign.RunsLog(c, $"Propuesta rechazada: {p.Text}.", LogTones.Bad);
                return CampaignResult.Success();
            }
            var action = (RunAction)p.Action;
            var error = Refusal(c, action, ctx.Connected);
            if (error != null)
            {
                Campaign.RunsLog(c, $"Propuesta aceptada, pero ya no se puede: {p.Text}. {error}", LogTones.Bad);
                return CampaignResult.Success();
            }
            return Apply(c, action, ctx);
        }
    }
}
