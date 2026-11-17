using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Net
{
    /// <summary>
    /// What a player's app has heard of the game, kept as its screen keeps it: the room, the campaign, and the mission
    /// (its snapshot with the events since applied, on a copy of its own; none while the room isn't in one). For the
    /// tests between two apps (the app's CampaignRun, NetCampaignRunTests): MissionBot plays on it, with Active for who
    /// counts as here, and BaseOrder says what to do on the base. Only reads what it is given.
    /// </summary>
    public sealed class HeardGame
    {
        private readonly IClock clock;

        /// <summary>When each player not connected now was heard to drop (the clock's ms).</summary>
        private readonly Dictionary<int, long> droppedAt = new Dictionary<int, long>();

        /// <summary>The Trades this app's player has bought at (the run's seed and the node): one ware each (RunOrder).</summary>
        private readonly HashSet<string> bought = new HashSet<string>();

        public HeardGame(IClock? clock = null)
        {
            this.clock = clock ?? new SystemClock();
        }

        public RoomMessage? Room { get; private set; }

        /// <summary>The campaign as last sent (the very object: read it only).</summary>
        public CampaignState? Campaign { get; private set; }

        /// <summary>The mission now, a copy of its own; null outside one, and after a batch of events missed until the next snapshot.</summary>
        public GameState? Mission { get; private set; }

        public void Observe(ServerMessage msg)
        {
            switch (msg)
            {
                case RoomMessage room:
                    foreach (var p in room.Players)
                    {
                        if (p.Connected) droppedAt.Remove(p.Slot);
                        // The host says how long they still count (also for one never seen here: a mission opened again without them).
                        else if (p.GraceMs != null) droppedAt[p.Slot] = clock.Now + p.GraceMs.Value - Grace(p);
                        else if (Room?.Players.FirstOrDefault((q) => q.Slot == p.Slot)?.Connected == true) droppedAt[p.Slot] = clock.Now;
                    }
                    Room = room;
                    // Out of the mission: what was heard of it is over (the next one comes with its snapshot).
                    if (room.Phase != RoomPhases.Mission) Mission = null;
                    break;
                case CampaignMessage campaign:
                    Campaign = campaign.State;
                    break;
                case SnapshotMessage snapshot:
                    // A copy: the screen applies the events to the one it gets.
                    Mission = snapshot.State == null ? null : Json.Read<GameState>(Json.Write(snapshot.State));
                    break;
                case EventsMessage events:
                    if (Mission == null) break;
                    // A batch missed: the whole state comes again (ServerFeed asks for it), and the copy waits for it.
                    if (Mission.Seq + events.Events.Count != events.Seq)
                    {
                        Mission = null;
                        break;
                    }
                    foreach (var e in Json.Read<List<GameEvent>>(Json.Write(events.Events))) Events.ApplyEvent(Mission, e);
                    break;
            }
        }

        /// <summary>
        /// The players who count as here (Room.ActiveSlots, as the battle screen keeps them): connected, or dropped within
        /// the host's grace (Room.ReconnectGraceMs; AwayGraceMs if their game was out of sight), from when this app heard
        /// it. Till then the host keeps the dropped one's soldiers and command theirs: MissionBot plays with these, not
        /// with the connected (29/10: it tried the dropped partner's soldiers every 0.6 s, «Es de la escuadra del otro
        /// jugador.»).
        /// </summary>
        public List<int> Active()
        {
            var now = clock.Now;
            return (Room?.Players ?? new List<PlayerInfo>())
                .Where((p) => p.Connected || (droppedAt.TryGetValue(p.Slot, out var at) && now - at < Grace(p)))
                .Select((p) => p.Slot)
                .ToList();
        }

        private static long Grace(PlayerInfo p) => p.Away == true ? Bastion.Net.Room.AwayGraceMs : Bastion.Net.Room.ReconnectGraceMs;

        /// <summary>
        /// What `slot` does on the base before launching: the partner's proposal, yes; an event, the host's first option it
        /// can take (the convoy's wants someone fit to send); with no operation chosen, a day's wait while someone is
        /// wounded, then the host's proposal of the easiest (a day's wait with none); with none of its soldiers in the squad
        /// and one fit to go (the recruit who comes when nobody is left: Arturo, 2/11), that one in; with nobody in the squad
        /// at all, a day's wait till someone heals or comes, or in a siege (it takes everyone fit by itself, and nobody can
        /// wait) the host's recruit straight into it; else its key to launch. Null: wait (one's own proposal, the partner's
        /// event or choice). In a campaign of runs, RunOrder.
        /// </summary>
        public CampaignCommand? BaseOrder(int slot, bool hosting)
        {
            var c = Campaign;
            if (c == null) return null;
            if (Engine.Campaign.IsRuns(c)) return RunOrder(c, slot, hosting);
            if (c.Proposal != null) return c.Proposal.By != slot ? new VoteCommand { Accept = true } : null;
            if (c.Event != null) return hosting ? new AnswerEventCommand { Option = Engine.Campaign.EventOptionAvailable(c, 0) || !Engine.Campaign.EventOptionAvailable(c, 1) ? 0 : 1 } : null;
            // No operation chosen (no siege comes to choose it now: Arturo, 7/11, Campaign.SiegesOn): with someone wounded, a
            // day's wait till all are fit, since the wounded don't go (of seeds 1 to 200, 47 won the opening operation; the
            // operation after it was won once going without them, with one to three soldiers, and 5 times waiting); then the
            // host proposes the easiest one there is, and the partner says yes; with none, a day's wait for more.
            if (c.ActiveMission == null)
            {
                if (c.Soldiers.Any((s) => s.Alive && s.WoundedDays > 0)) return new AdvanceCommand { Ready = true };
                var easiest = c.Offers.Where((o) => !o.Final && o.Facility == null && o.Siege != true && (o.ExpiresDay == null || o.ExpiresDay >= c.Day))
                    .OrderBy((o) => o.Difficulty).ThenBy((o) => o.Id, System.StringComparer.Ordinal).FirstOrDefault();
                if (easiest == null) return new AdvanceCommand { Ready = true };
                return hosting ? new ChooseMissionCommand { Id = easiest.Id } : null;
            }
            var siege = c.Offers.FirstOrDefault((o) => o.Id == c.ActiveMission)?.Siege == true;
            if (c.ActiveMission != null && !siege && c.Squad.Count < Engine.Campaign.SquadMax(c))
            {
                var mine = c.Soldiers.Where((s) => s.Owner == slot).ToList();
                var left = mine.FirstOrDefault((s) => Engine.Campaign.SoldierReady(s) && !c.Squad.Contains(s.Id));
                if (left != null && !mine.Any((s) => c.Squad.Contains(s.Id))) return new SquadCommand { Soldier = left.Id, On = true };
            }
            if (c.ActiveMission != null && c.Squad.Count == 0) return !siege ? new AdvanceCommand { Ready = true } : hosting ? new RecruitCommand { Squad = true } : null;
            return new LaunchCommand { Ready = true };
        }

        /// <summary>
        /// BaseOrder in the runs (H0-DISENO §7; Gameplay, 16/11): the partner's proposal, yes; each its soldiers' kit
        /// (KitOrder); then, by where the run is. On the base, each puts its soldiers alive in the squad while there are seats,
        /// then the host proposes an upgrade it can pay (UpgradeOrder), else going out. On the map, a medicine to one
        /// wounded enough (MedicineOrder), then the host proposes the first node in sight. At a stop, the host answers yes
        /// (a survivor with no bed for them, no); at a Trade each buys one ware (WareOrder); then each asks to go on, again
        /// if the partner has gone meanwhile (alone, it counts at once). Back to the base before the boss only when the squad
        /// is broken (Homeward); else the run comes back by itself after it, or when the squad falls. In a fight, nothing. Each order is one the engine would take now, by
        /// its own refusals on what was heard. Null: wait.
        /// </summary>
        private CampaignCommand? RunOrder(CampaignState c, int slot, bool hosting)
        {
            if (c.Proposal != null) return c.Proposal.By != slot ? new VoteCommand { Accept = true } : null;
            var here = Active();
            var run = c.Run;
            if (run?.Phase == RunPhases.Fight) return null;
            var kit = KitOrder(c, slot, here);
            if (kit != null) return kit;
            if (run == null)
            {
                // Its soldiers alive into the squad while there are seats (a recruit after a wipe stayed out: R2, 17/11).
                foreach (var s in c.Soldiers.Where((x) => x.Owner == slot && x.Alive && !c.Squad.Contains(x.Id)))
                {
                    var join = new SquadCommand { Soldier = s.Id, On = true };
                    if (Runs.SquadRefusal(c, slot, here, join) == null) return join;
                }
                if (!hosting) return null;
                return UpgradeOrder(c) ?? (Runs.StartRunRefusal(c, here) == null ? new StartRunCommand() : null);
            }
            if (run.Phase == RunPhases.Choose)
            {
                var medicine = MedicineOrder(c, slot, here);
                if (medicine != null) return medicine;
                if (!hosting) return null;
                if (run.Path.Count == 0) wentOut[run.Seed] = run.Squad.Count;
                if (Homeward(c, run)) return Runs.EndRunRefusal(c) == null ? new EndRunCommand() : null;
                var choices = RunMap.Choices(run);
                var next = choices.FirstOrDefault((n) => RunMap.Visible(c, n)) ?? choices.FirstOrDefault();
                return next != null && Runs.ChooseNodeRefusal(c, next.Id) == null ? new ChooseNodeCommand { Node = next.Id } : null;
            }
            var stop = run.Stop;
            if (run.Phase != RunPhases.Stop || stop == null) return null;
            if (Runs.LeaveStopRefusal(c) != null)
            {
                if (!hosting) return null;
                var option = Runs.AnswerStopRefusal(c, 0) == null ? 0 : 1;
                return Runs.AnswerStopRefusal(c, option) == null ? new AnswerStopCommand { Option = option } : null;
            }
            var ware = WareOrder(c, slot, here);
            if (ware != null) return ware;
            var asked = stop.Leaving.Length > slot && stop.Leaving[slot];
            return asked && here.Any((p) => p != slot) ? null : new LeaveStopCommand { Ready = true };
        }

        /// <summary>
        /// The upgrades the bots save for, each to its level, in this order (Red, 17/11, for H8's bench: what helps the squad
        /// most against El Toro first): more soldiers out, mending after each node, a bigger pack, then the rest.
        /// </summary>
        private static readonly (string Upgrade, int Level)[] Upgrades =
        {
            (UpgradeIds.Squad, 1), (UpgradeIds.Mend, 1), (UpgradeIds.Pack, 1), (UpgradeIds.Squad, 2), (UpgradeIds.Pack, 2),
            (UpgradeIds.Sight, 1), (UpgradeIds.Haggle, 1), (UpgradeIds.Stash, 1), (UpgradeIds.Sight, 2), (UpgradeIds.Stash, 2),
            (UpgradeIds.Stash, 3), (UpgradeIds.Squad, 3),
        };

        /// <summary>On the base, the next upgrade of Upgrades not bought yet, proposed once the stash can pay it. Null: saving, or all bought.</summary>
        private static CampaignCommand? UpgradeOrder(CampaignState c)
        {
            var next = Upgrades.FirstOrDefault((u) => Runs.Level(c, u.Upgrade) < u.Level);
            return next.Upgrade != null && Runs.UpgradeRefusal(c, next.Upgrade) == null ? new UpgradeCommand { Upgrade = next.Upgrade } : null;
        }

        /// <summary>How many went out on each run (its seed) this app saw start: so that Homeward knows how many fell.</summary>
        private readonly Dictionary<int, int> wentOut = new Dictionary<int, int>();

        /// <summary>
        /// Whether the squad goes back to the base from the map (Red, 17/11, for H8's bench: as a careful player would, with
        /// what it found): after a fight, once half of those who went out have fallen (half the seats, if this app didn't see
        /// it go out), or those left have half their health or less. Back, all are healed and the packs come home.
        /// </summary>
        private bool Homeward(CampaignState c, RunState run)
        {
            if (run.Path.Count == 0) return false;
            var left = run.Squad.Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)).Where((s) => s != null && s.Alive).Select((s) => s!).ToList();
            var went = wentOut.TryGetValue(run.Seed, out var n) ? n : Runs.SeatsOf(c);
            if (left.Count * 2 <= went) return true;
            return left.Sum((s) => Runs.HpOf(c, s)) * 2 <= left.Sum(Runs.MaxHp);
        }

        /// <summary>The soldiers `slot` sees to: those it controls (Runs.Controls) of the squad out, or, on the base, of the one that will go.</summary>
        private static List<CampaignSoldier> Mine(CampaignState c, int slot, IReadOnlyList<int> here)
        {
            var squad = c.Run?.Squad ?? c.Squad;
            return c.Soldiers.Where((s) => s.Alive && s.Kit != null && squad.Contains(s.Id) && Runs.Controls(c, slot, here, s)).ToList();
        }

        /// <summary>Kit places a bot fills, in this order (the blade is the knife already).</summary>
        private static readonly string[] Fills = { KitSlots.Primary, KitSlots.Pistol, KitSlots.Vest, KitSlots.Belt0, KitSlots.Belt1 };

        /// <summary>
        /// One move of `slot`'s soldiers' kit, if there's one to make: an empty place filled from the soldier's own pack or,
        /// on the base, from the stash; on the base, then, what the pack carries for no use (weapons, vests, belt things, mods
        /// it doesn't wear) and the supplies (Red, 17/11: the upgrades are paid from the stash's) put in the stash, and a pile
        /// of magazines and one of medicines from the stash into a pack without any. A Trade is paid with what the run finds.
        /// Null if nothing is to move.
        /// </summary>
        private static CampaignCommand? KitOrder(CampaignState c, int slot, IReadOnlyList<int> here)
        {
            var atBase = c.Run == null;
            var stash = atBase ? c.Stash?.Items.Select((p) => p.Gear).ToList() ?? new List<Gear>() : new List<Gear>();
            CampaignCommand? Try(MoveGearCommand cmd) => Gears.MoveRefusal(c, slot, here, cmd) == null ? cmd : null;
            var mine = Mine(c, slot, here);
            foreach (var s in mine)
                foreach (var place in Fills)
                {
                    if (Gears.InSlot(s.Kit!, place) != null) continue;
                    foreach (var g in s.Kit!.Pack.Items.Select((p) => p.Gear).Concat(stash).Where((g) => Content.SlotTakes(place, g)))
                    {
                        var order = Try(new MoveGearCommand { Gear = g.Id, To = s.Id, Slot = place });
                        if (order != null) return order;
                    }
                }
            if (!atBase || c.Stash == null) return null;
            bool Kept(Gear g) => g.Kind == GearKinds.Medicine || g.Kind == GearKinds.Mags;
            foreach (var s in mine)
                foreach (var g in s.Kit!.Pack.Items.Select((p) => p.Gear).Where((g) => !Kept(g)).ToList())
                {
                    // Supplies onto a pile of them with room first, so the stash's few rows hold them.
                    var spot = c.Stash.Items.FirstOrDefault((p) => Gears.SamePile(p.Gear, g) && Gears.Room(p.Gear) > 0) ?? Gears.FirstFit(c.Stash, g);
                    var order = spot != null ? Try(new MoveGearCommand { Gear = g.Id, To = GearPlaces.Stash, Slot = KitSlots.Pack, X = spot.X, Y = spot.Y, Turned = spot.Turned }) : null;
                    if (order != null) return order;
                }
            foreach (var kind in new[] { GearKinds.Mags, GearKinds.Medicine })
                foreach (var s in mine.Where((s) => !s.Kit!.Pack.Items.Any((p) => p.Gear.Kind == kind)))
                {
                    var g = stash.FirstOrDefault((x) => x.Kind == kind);
                    var spot = g != null ? Gears.FirstFit(s.Kit!.Pack, g) : null;
                    var order = spot != null ? Try(new MoveGearCommand { Gear = g!.Id, To = s.Id, Slot = KitSlots.Pack, X = spot.X, Y = spot.Y, Turned = spot.Turned }) : null;
                    if (order != null) return order;
                }
            return null;
        }

        /// <summary>
        /// On the map, a medicine from a pack of `slot`'s soldiers to the one of them most hurt, if it lacks at least what one
        /// heals (Content.MedicineHeal). Null if none.
        /// </summary>
        private static CampaignCommand? MedicineOrder(CampaignState c, int slot, IReadOnlyList<int> here)
        {
            var mine = Mine(c, slot, here);
            var hurt = mine.Select((s) => (s, lack: Runs.MaxHp(s) - Runs.HpOf(c, s))).Where((x) => x.lack >= Content.MedicineHeal)
                .OrderByDescending((x) => x.lack).ThenBy((x) => x.s.Id, System.StringComparer.Ordinal).Select((x) => x.s).FirstOrDefault();
            if (hurt == null) return null;
            var medicine = mine.SelectMany((s) => s.Kit!.Pack.Items).Select((p) => p.Gear).FirstOrDefault((g) => g.Kind == GearKinds.Medicine);
            if (medicine == null) return null;
            var cmd = new UseGearCommand { Gear = medicine.Id, Soldier = hurt.Id };
            return Gears.UseGearRefusal(c, slot, here, cmd) == null ? cmd : null;
        }

        /// <summary>
        /// At a Trade, one ware for `slot` once: a firearm if one of its soldiers lacks one, else a medicine, else the
        /// cheapest; for the first of its soldiers that can take it and pay. Null if bought already, or none can be.
        /// </summary>
        private CampaignCommand? WareOrder(CampaignState c, int slot, IReadOnlyList<int> here)
        {
            var run = c.Run!;
            var wares = run.Stop?.Wares;
            var key = $"{run.Seed}/{run.At}/{slot}";
            if (wares == null || bought.Contains(key)) return null;
            var mine = Mine(c, slot, here);
            var unarmed = mine.Any((s) => s.Kit!.Primary == null && s.Kit.Pistol == null);
            int Want(Ware w) => unarmed && (Content.SlotTakes(KitSlots.Primary, w.Gear) || Content.SlotTakes(KitSlots.Pistol, w.Gear)) ? 0 : w.Gear.Kind == GearKinds.Medicine ? 1 : 2;
            foreach (var w in wares.OrderBy(Want).ThenBy((w) => w.Price).ThenBy((w) => w.Gear.Id, System.StringComparer.Ordinal))
                foreach (var s in mine)
                {
                    var cmd = new BuyWareCommand { Ware = w.Gear.Id, Soldier = s.Id };
                    if (Gears.BuyWareRefusal(c, slot, here, cmd) != null) continue;
                    bought.Add(key);
                    return cmd;
                }
            return null;
        }
    }
}
