#nullable enable
// The runs (.dev-snapshots/gameplay/runs/H0-DISENO.md §3–5; new in C#): the base's numbers and upgrades, who controls
// which soldier, health in a run, the squad that goes out, and what each joint decision of the runs refuses and does:
// going out, choosing a node, going back, the stops (Refuge, Trade, Event; H4 brings their real contents; N3 took the Survivor out)
// and the upgrades. The commands that reach them are in RunCommands.cs; the gear's, in Gears.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Runs
    {
        // ---------------------------------------------------------------- numbers

        /// <summary>The stash at the start, 10×6; each «stash» level adds StashRows rows.</summary>
        public const int StashW = 10;
        public const int StashH = 6;
        public const int StashRows = 2;
        /// <summary>A pack at the start, 4×3; each «pack» level adds a row.</summary>
        public const int PackW = 4;
        public const int PackH = 3;
        /// <summary>
        /// Soldiers who go out, by «squad» level: from 2, on the motorbike (N4, Arturo 17/11: «se empieza con 2»; with two
        /// players, one each), to 4, 6 and 8.
        /// </summary>
        public static readonly IReadOnlyList<int> Seats = new[] { 2, 4, 6, 8 };
        /// <summary>Health the «mend» upgrade gives everyone after each node.</summary>
        public const int MendHeal = 2;
        /// <summary>Chapters in the game (only the first in this version).</summary>
        public const int Chapters = 3;
        /// <summary>Soldiers each player starts with: two, or four alone.</summary>
        public const int StartSoldiers = 2;

        public static int Level(CampaignState c, string upgrade)
        {
            return c.Upgrades != null && c.Upgrades.TryGetValue(upgrade, out var level) ? level : 0;
        }

        /// <summary>What the next level of an upgrade costs, or null at the top.</summary>
        public static int? NextPrice(CampaignState c, string upgrade)
        {
            if (!Content.UpgradePrices.TryGetValue(upgrade, out var prices)) return null;
            var level = Level(c, upgrade);
            // A bench's first price (Tuning).
            if (level == 0 && Tuning.IsSet && Tuning.Current.UpgradePrices != null && Tuning.Current.UpgradePrices.TryGetValue(upgrade, out var first)) return first;
            return level < prices.Count ? prices[level] : (int?)null;
        }

        /// <summary>The § in the stash: its supplies piles.</summary>
        public static int StashSupplies(CampaignState c)
        {
            return c.Stash != null ? Gears.Supplies(c.Stash) : 0;
        }

        /// <summary>Soldiers who go out: 2, 4, 6 or 8.</summary>
        public static int SeatsOf(CampaignState c)
        {
            return Seats[Math.Min(Level(c, UpgradeIds.Squad), Seats.Count - 1)];
        }

        /// <summary>
        /// Whether La Lata has a seat the squad doesn't fill (N2): with none, a Rescate's survivor won't fit on the way out
        /// unless one of the squad falls, and the map says so before going in (Lore.RescueNoSeat). The run's squad standing,
        /// or on the base the one that would go out.
        /// </summary>
        public static bool SeatFree(CampaignState c)
        {
            var squad = c.Run?.Squad ?? c.Squad;
            return squad.Count((id) => c.Soldiers.Any((s) => s.Id == id && s.Alive)) < SeatsOf(c);
        }

        /// <summary>
        /// Most soldiers each player takes: half the seats with the partner here, all of them alone (as Campaign.SquadLimit)
        /// or with a partner left with nobody alive (N8: the game counts, not the player).
        /// </summary>
        public static int SquadLimit(CampaignState c, IReadOnlyList<int> connected)
        {
            var seats = SeatsOf(c);
            return c.Players.Count > 1 && Bringers(c, connected).Count > 1 ? (seats + 1) / 2 : seats;
        }

        /// <summary>
        /// The players whose soldiers go out: those here with one alive; none of them with any, every player's (N8: one alone
        /// with nobody of their own takes the partner's out, and commands them while the partner is away: Controls).
        /// </summary>
        private static List<int> Bringers(CampaignState c, IReadOnlyList<int> connected)
        {
            bool Has(int p) => c.Soldiers.Any((s) => s.Owner == p && s.Alive);
            var here = c.Players.Where((p) => connected.Contains(p) && Has(p)).ToList();
            return here.Count > 0 ? here : c.Players.Where(Has).ToList();
        }

        public static int PackRows(CampaignState c)
        {
            return PackH + Level(c, UpgradeIds.Pack);
        }

        /// <summary>A new soldier's kit: the knife, and the pack empty at the size the upgrades give.</summary>
        public static Kit NewKit(CampaignState c)
        {
            return new Kit { Melee = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Knife), Pack = new GearGrid { W = PackW, H = PackRows(c) } };
        }

        // ------------------------------------------------------------- the soldiers

        /// <summary>
        /// Whether `slot` commands a soldier (Red and Gameplay, 16/11): the one it was handed to in this run (RunState.hands)
        /// or else its owner; anyone, if that player isn't connected.
        /// </summary>
        public static bool Controls(CampaignState c, int slot, IReadOnlyList<int> connected, CampaignSoldier s)
        {
            var by = c.Run?.Hands != null && c.Run.Hands.TryGetValue(s.Id, out var hand) ? hand : s.Owner;
            return by == slot || !connected.Contains(by);
        }

        /// <summary>A soldier's most health in a run: its bars and board (Bars.FighterStatsOf) and its vest.</summary>
        public static int MaxHp(CampaignSoldier s)
        {
            var vest = s.Kit?.Vest?.Type != null && Content.Vests.TryGetValue(s.Kit.Vest.Type, out var def) ? def.Hp : 0;
            return Bars.FighterStatsOf(s.Skills, s.Growth, s.Board).MaxHp + vest;
        }

        /// <summary>A soldier's armour from its kit: the heavy vest's.</summary>
        public static int ArmorOf(CampaignSoldier s)
        {
            return s.Kit?.Vest?.Type != null && Content.Vests.TryGetValue(s.Kit.Vest.Type, out var def) ? def.Armor : 0;
        }

        /// <summary>The health a soldier has: what is left in the run, or all of it in the base.</summary>
        public static int HpOf(CampaignState c, CampaignSoldier s)
        {
            // N9, Supervivencia: the wounds it came home with (CampaignSoldier.Hurt).
            return c.Run != null && c.Run.Hp.TryGetValue(s.Id, out var hp) ? hp : Math.Max(1, MaxHp(s) - (Levels.Of(c).Wounds ? s.Hurt ?? 0 : 0));
        }

        /// <summary>The squad that goes out by default: each connected player's living soldiers, as many as they may take.</summary>
        public static List<string> DefaultSquad(CampaignState c, IReadOnlyList<int> connected)
        {
            var limit = SquadLimit(c, connected);
            var squad = new List<string>();
            foreach (var slot in Bringers(c, connected))
            {
                squad.AddRange(c.Soldiers.Where((s) => s.Owner == slot && s.Alive).Take(limit).Select((s) => s.Id));
            }
            return squad.Take(SeatsOf(c)).ToList();
        }

        /// <summary>
        /// A player who joins a run with none of their soldiers in it (the room calls it): half the squad, rounded down and
        /// at least one if there are two, passes to them till the run ends (RunState.hands). How many.
        /// </summary>
        public static int HandOver(CampaignState c, int slot, IReadOnlyList<int> connected)
        {
            var run = c.Run;
            if (run == null || !connected.Contains(slot)) return 0;
            var squad = run.Squad.Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)).Where((s) => s != null && s.Alive).Select((s) => s!).ToList();
            if (squad.Any((s) => (run.Hands != null && run.Hands.TryGetValue(s.Id, out var hand) ? hand : s.Owner) == slot)) return 0;
            var n = squad.Count >= 2 ? Math.Max(1, squad.Count / 2) : 0;
            foreach (var s in squad.Skip(squad.Count - n)) (run.Hands ??= new Dictionary<string, int>())[s.Id] = slot;
            return n;
        }

        /// <summary>
        /// A new soldier with a knife (the free recruit, a survivor): one of Campaign's, with no arms of before and its kit.
        /// `join`: into the campaign's soldiers now.
        /// </summary>
        public static CampaignSoldier NewSoldier(CampaignState c, int owner, Rng rng, bool join = true)
        {
            var s = Campaign.RunsRecruit(c, owner, rng, join);
            s.Kit = NewKit(c);
            return s;
        }

        /// <summary>
        /// N8 (Arturo, 22/11, cuadro 118): nobody left alive in the game, out or at the base, and no run going, and the game
        /// is lost (CampaignState.outcome, defeat), at every level: no recruit for nothing. With two, the game counts, not the
        /// player: one left with nobody plays on with the partner's (SquadLimit, HandOver), and a survivor brought out goes to
        /// whoever has fewest (Rescued). A bench may give recruits instead, as before (Tuning.Wipe): a player left without a
        /// soldier alive gets one with a knife. True if anything changed.
        /// </summary>
        public static bool NobodyLeft(CampaignState c, Rng rng)
        {
            if (c.Outcome != null || c.Run != null) return false;
            if (Tuning.Current.Wipe == Tuning.Wipes.Lose)
            {
                if (c.Soldiers.Any((s) => s.Alive)) return false;
                c.Outcome = Outcomes.Defeat;
                Campaign.RunsLog(c, RunTexts.GameLost, LogTones.Bad);
                return true;
            }
            var came = new List<CampaignSoldier>();
            foreach (var owner in c.Players)
            {
                if (c.Soldiers.Any((s) => s.Owner == owner && s.Alive)) continue;
                var s = NewSoldier(c, owner, rng);
                Campaign.RunsLog(c, RunTexts.Recruit(s.Name), LogTones.Good);
                came.Add(s);
            }
            // A bench's (Tuning.Wipe): recruits till the seats are full, in turn to whoever has fewest; with a pistol each.
            if (came.Count > 0 && Tuning.Current.Wipe != Tuning.Wipes.One)
            {
                while (c.Players.Count > 0 && c.Soldiers.Count((s) => s.Alive) < SeatsOf(c))
                {
                    var owner = c.Players.OrderBy((p) => c.Soldiers.Count((s) => s.Owner == p && s.Alive)).ThenBy((p) => p).First();
                    var s = NewSoldier(c, owner, rng);
                    Campaign.RunsLog(c, RunTexts.Recruit(s.Name), LogTones.Good);
                    came.Add(s);
                }
                if (Tuning.Current.Wipe == Tuning.Wipes.FillPistol) foreach (var s in came) s.Kit!.Pistol = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol);
            }
            return came.Count > 0;
        }

        /// <summary>
        /// A partner who joins a run campaign started without them (Campaign.AddPlayer): the other's rookies that never went
        /// out and aren't in a run change hands, never leaving the other with fewer than two; recruits with a knife make up
        /// the rest of their two. How many they got.
        /// </summary>
        public static int AddPlayer(CampaignState c, int slot, Rng rng)
        {
            if (c.Players.Contains(slot)) return 0;
            c.Players = c.Players.Append(slot).OrderBy((p) => p).ToList();
            var rookies = new List<CampaignSoldier>();
            foreach (var s in Enumerable.Reverse(c.Soldiers.Where((x) => x.Owner != slot && x.Alive && x.Missions == 0 && c.Run?.Squad.Contains(x.Id) != true).ToList()))
            {
                if (rookies.Count >= StartSoldiers) break;
                if (c.Soldiers.Count((x) => x.Owner == s.Owner && x.Alive) - rookies.Count((r) => r.Owner == s.Owner) <= StartSoldiers) continue;
                rookies.Add(s);
            }
            foreach (var s in rookies)
            {
                s.Appearance = Appearances.DefaultAppearance(slot, null, s.Appearance.Sex, s.Id, s.Appearance.Wardrobe);
                s.Owner = slot;
            }
            c.Squad = c.Squad.Where((id) => !rookies.Any((s) => s.Id == id)).ToList();
            for (var i = rookies.Count; i < StartSoldiers; i++) NewSoldier(c, slot, rng);
            return StartSoldiers;
        }

        // --------------------------------------------------------------- the squad

        /// <summary>Why `slot` can't put a soldier in or out of the squad that goes out (SquadCommand, in the base), or null.</summary>
        public static string? SquadRefusal(CampaignState c, int slot, IReadOnlyList<int> connected, SquadCommand cmd)
        {
            if (c.Run != null) return RunTexts.RunOn;
            var s = c.Soldiers.FirstOrDefault((x) => x.Id == cmd.Soldier);
            // Each picks their own, the other here or not (#43), as Campaign's; one with nobody of their own alive, the
            // partner's while the partner is away (N8).
            if (s == null || s.Owner != slot && (connected.Contains(s.Owner) || c.Soldiers.Any((x) => x.Owner == slot && x.Alive))) return RunTexts.PickOwn;
            if (!s.Alive) return RunTexts.NoSoldier;
            if (!cmd.On || c.Squad.Contains(s.Id)) return null;
            var limit = SquadLimit(c, connected);
            if (c.Squad.Count((id) => c.Soldiers.FirstOrDefault((x) => x.Id == id)?.Owner == s.Owner) >= limit) return RunTexts.PerPlayer(limit);
            return c.Squad.Count >= SeatsOf(c) ? RunTexts.Seats(SeatsOf(c)) : null;
        }

        private static void Squad(CampaignState c, SquadCommand cmd)
        {
            if (!cmd.On) c.Squad = c.Squad.Where((id) => id != cmd.Soldier).ToList();
            else if (!c.Squad.Contains(cmd.Soldier)) c.Squad.Add(cmd.Soldier);
        }

        // -------------------------------------------------------------- upgrades

        /// <summary>Why the next level of an upgrade can't be bought now: «Faltan 40 §», at the top, in a run. Null if it can.</summary>
        public static string? UpgradeRefusal(CampaignState c, string upgrade)
        {
            if (!Content.UpgradePrices.ContainsKey(upgrade)) return RunTexts.NoUpgrade;
            if (c.Run != null) return RunTexts.UpgradeAtBase;
            var price = NextPrice(c, upgrade);
            if (price == null) return RunTexts.UpgradeTop;
            var have = StashSupplies(c);
            return have < price.Value ? RunTexts.Missing(price.Value - have) : null;
        }

        /// <summary>The next level, paid from the stash's supplies: the stash grows 2 rows, every pack 1.</summary>
        private static void Upgrade(CampaignState c, string upgrade)
        {
            Gears.PaySupplies(new[] { c.Stash! }, NextPrice(c, upgrade)!.Value);
            var level = Level(c, upgrade) + 1;
            (c.Upgrades ??= new Dictionary<string, int>())[upgrade] = level;
            if (upgrade == UpgradeIds.Stash) c.Stash!.H += StashRows;
            if (upgrade == UpgradeIds.Pack)
            {
                foreach (var s in c.Soldiers.Where((x) => x.Kit != null)) s.Kit!.Pack.H += 1;
            }
            Campaign.RunsLog(c, RunTexts.Upgraded(upgrade, level), LogTones.Good);
        }

        // ------------------------------------------------------------- going out

        /// <summary>The squad as it would go out: its living soldiers.</summary>
        private static List<CampaignSoldier> Going(CampaignState c)
        {
            return c.Squad.Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)).Where((s) => s != null && s.Alive && s.Kit != null).Select((s) => s!).ToList();
        }

        /// <summary>Why the squad can't go out now (StartRunCommand), or null.</summary>
        public static string? StartRunRefusal(CampaignState c, IReadOnlyList<int> connected)
        {
            if (c.Run != null) return RunTexts.RunOn;
            var going = Going(c);
            if (going.Count == 0) return RunTexts.EmptySquad;
            if (going.Count > SeatsOf(c)) return RunTexts.Seats(SeatsOf(c));
            var limit = SquadLimit(c, connected);
            if (c.Players.Any((p) => going.Count((s) => s.Owner == p) > limit)) return RunTexts.PerPlayer(limit);
            return null;
        }

        /// <summary>The run of the chapter open: its map from a new seed, the squad at full health.</summary>
        private static void StartRun(CampaignState c, Rng rng)
        {
            var chapter = c.Chapter ?? 1;
            var seed = Campaign.RunsSeed(rng);
            var going = Going(c);
            c.Run = new RunState
            {
                Chapter = chapter,
                Seed = seed,
                Nodes = RunMap.Generate(chapter, seed),
                At = null,
                Path = new List<string>(),
                Phase = RunPhases.Choose,
                Squad = going.Select((s) => s.Id).ToList(),
                // N9, Supervivencia: out with the wounds they came home with (CampaignSoldier.Hurt).
                Hp = going.ToDictionary((s) => s.Id, (s) => Math.Max(1, MaxHp(s) - (Levels.Of(c).Wounds ? s.Hurt ?? 0 : 0))),
                HomeFromRefuge = true,
            };
            Campaign.RunsLog(c, RunTexts.RunStarted(chapter));
        }

        // ------------------------------------------------------------ on the map

        /// <summary>Why the squad can't go to that node (ChooseNodeCommand): only one the node it is at leads to, and in sight.</summary>
        public static string? ChooseNodeRefusal(CampaignState c, string node)
        {
            var run = c.Run;
            if (run == null) return RunTexts.NoRun;
            if (run.Phase != RunPhases.Choose) return RunTexts.NotChoosing;
            var n = RunMap.Node(run, node);
            if (n == null) return RunTexts.NoNode;
            if (!RunMap.Choices(run).Contains(n)) return RunTexts.NotNext;
            return RunMap.Visible(c, n) ? null : RunTexts.NotSeen;
        }

        /// <summary>To the node: a fight launches (true), a stop is set out. Either way it counts as a node played.</summary>
        private static bool ChooseNode(CampaignState c, string node, Rng rng)
        {
            var run = c.Run!;
            var n = RunMap.Node(run, node)!;
            run.At = n.Id;
            run.Path.Add(n.Id);
            c.NodesPlayed = (c.NodesPlayed ?? 0) + 1;
            if (RunMap.IsFight(n.Kind))
            {
                // What an event left for the next fight (H4), besides the node's own.
                if (run.Pending != null)
                {
                    n.Added = run.Pending;
                    run.Pending = null;
                }
                // TODO(H3): the room launches it (EngineCampaignRules.LaunchMission builds the node's mission from its site,
                // seed, difficulty and modifier, and the kits; ApplyMissionResult folds it back and calls Runs.NodeCleared).
                // Till then LaunchMission finds no operation chosen and gives null: the run waits in the fight.
                run.Phase = RunPhases.Fight;
                run.Stop = null;
                return true;
            }
            run.Phase = RunPhases.Stop;
            run.Stop = MakeStop(c, n, rng);
            return false;
        }

        /// <summary>
        /// Why the squad can't go back now (EndRunCommand): only from a Refuge, at its stop (G22, Arturo 18/11, cuadro 72;
        /// the boss dead, the run comes home by itself); a run started before (RunState.HomeFromRefuge missing), from the
        /// map between nodes, as then.
        /// </summary>
        public static string? EndRunRefusal(CampaignState c)
        {
            var run = c.Run;
            if (run == null) return RunTexts.NoRun;
            if (run.HomeFromRefuge == true) return AtRefuge(run) ? null : RunTexts.EndFromRefuge;
            return run.Phase != RunPhases.Choose ? RunTexts.EndFromMap : null;
        }

        /// <summary>Whether the squad is at a Refuge's stop (G22: the way home).</summary>
        public static bool AtRefuge(RunState run) => run.Phase == RunPhases.Stop && RunMap.Node(run, run.At)?.Kind == NodeKinds.Refuge;

        /// <summary>
        /// The run ends: back to the base, everyone healed (health is only counted in a run), each pack with what it holds;
        /// the soldiers handed over go back to their owner. Nobody left alive in the game, it is lost (NobodyLeft, N8).
        /// </summary>
        public static void EndRun(CampaignState c, Rng rng, string? line = null)
        {
            // N9, Supervivencia: the wounds come home with them (healed only at a Refuge, with medicines, with the upgrade).
            if (c.Run != null && Levels.Of(c).Wounds)
                foreach (var s in c.Run.Squad.Select((id) => c.Soldiers.FirstOrDefault((x) => x.Id == id)).Where((s) => s != null && s.Alive))
                {
                    var hurt = MaxHp(s!) - HpOf(c, s!);
                    s!.Hurt = hurt > 0 ? hurt : (int?)null;
                }
            c.Run = null;
            c.Squad = c.Squad.Where((id) => c.Soldiers.Any((s) => s.Id == id && s.Alive)).ToList();
            Campaign.RunsLog(c, line ?? (Levels.Of(c).Wounds ? RunTexts.RunEndedHurt : RunTexts.RunEnded), LogTones.Good);
            NobodyLeft(c, rng);
        }

        /// <summary>After each node: the «mend» upgrade heals everyone a little.</summary>
        private static void AfterNode(CampaignState c)
        {
            var run = c.Run!;
            if (Level(c, UpgradeIds.Mend) == 0) return;
            foreach (var s in run.Squad.Select((id) => c.Soldiers.FirstOrDefault((x) => x.Id == id)).Where((s) => s != null && s.Alive))
            {
                run.Hp[s!.Id] = Math.Min(MaxHp(s), HpOf(c, s) + MendHeal);
            }
        }

        /// <summary>
        /// A fight won and left (H3's ApplyMissionResult calls it once it has folded the mission in; a lab calls it to win a
        /// node by itself): back to the map, or, the boss dead, the chapter done and the squad home.
        /// </summary>
        public static void NodeCleared(CampaignState c, Rng rng)
        {
            var run = c.Run;
            if (run == null || run.Phase != RunPhases.Fight) return;
            if (RunMap.Node(run, run.At)?.Kind == NodeKinds.Boss)
            {
                var chapter = run.Chapter;
                c.ChaptersDone = Math.Max(c.ChaptersDone ?? 0, chapter);
                c.Chapter = Math.Min(Chapters, chapter + 1);
                EndRun(c, rng, RunTexts.ChapterDone(chapter));
                return;
            }
            run.Phase = RunPhases.Choose;
            AfterNode(c);
        }

        // ----------------------------------------------------------------- stops

        /// <summary>What a stop holds on arrival: the Refuge heals, the Trade sets out its wares, an event waits.</summary>
        private static StopState MakeStop(CampaignState c, RunNode n, Rng rng)
        {
            var run = c.Run!;
            var stop = new StopState();
            switch (n.Kind)
            {
                case NodeKinds.Refuge:
                    foreach (var s in run.Squad.Select((id) => c.Soldiers.FirstOrDefault((x) => x.Id == id)).Where((s) => s != null && s.Alive)) run.Hp[s!.Id] = MaxHp(s);
                    stop.Outcome = "refuge.rest";
                    break;
                case NodeKinds.Trade:
                    stop.Wares = Wares(c, rng);
                    break;
                case NodeKinds.Event:
                    stop.Event = PickEvent(run);
                    break;
            }
            return stop;
        }

        /// <summary>The run's own dice (H4): drawn from its seed and how far it has gone, the same for the same run.</summary>
        private static Rng RunRng(RunState run, int salt)
        {
            return new Rng(unchecked(run.Seed * 31 + run.Path.Count * 7919 + salt));
        }

        /// <summary>An event the run hasn't met (all met, any again), from its seed (RunState.events).</summary>
        private static string PickEvent(RunState run)
        {
            var met = run.Events ??= new List<string>();
            var left = Content.RunEventIds.Where((id) => !met.Contains(id)).ToList();
            if (left.Count == 0) left = Content.RunEventIds.ToList();
            var id = left[RunRng(run, 0x5e).Int(left.Count)];
            met.Add(id);
            return id;
        }

        /// <summary>The squad's soldiers standing, with their kits (whose packs an event pays from and fills).</summary>
        private static List<CampaignSoldier> Standing(CampaignState c)
        {
            return c.Run!.Squad.Select((id) => c.Soldiers.FirstOrDefault((x) => x.Id == id)).Where((s) => s != null && s.Alive && s.Kit != null).Select((s) => s!).ToList();
        }

        /// <summary>Content.TradeCount wares from the price list, none twice, the more common more often; haggled with the upgrade.</summary>
        private static List<Ware> Wares(CampaignState c, Rng rng)
        {
            var left = Content.TradeWares.ToList();
            var wares = new List<Ware>();
            var haggle = Level(c, UpgradeIds.Haggle) > 0;
            while (wares.Count < Content.TradeCount && left.Count > 0)
            {
                var roll = rng.Int(left.Sum((x) => x.Weight));
                var pick = left[left.Count - 1];
                foreach (var w in left)
                {
                    if (roll < w.Weight)
                    {
                        pick = w;
                        break;
                    }
                    roll -= w.Weight;
                }
                left.Remove(pick);
                wares.Add(new Ware { Gear = Gears.NewGear(c, pick.Kind, pick.Type, pick.Count), Price = Content.Haggled(pick.Price, haggle) });
            }
            return wares;
        }

        /// <summary>
        /// Why the stop can't be answered so (AnswerStopCommand): only an event, once, 0 or 1; `node`, the stop the answer
        /// was given at (a vote left over from another is refused).
        /// </summary>
        public static string? AnswerStopRefusal(CampaignState c, int option, string? node = null)
        {
            var run = c.Run;
            if (run == null || run.Phase != RunPhases.Stop || run.Stop == null) return RunTexts.NotAtStop;
            if (node != null && node != run.At) return RunTexts.StaleAnswer;
            var kind = RunMap.Node(run, run.At)?.Kind;
            if (kind != NodeKinds.Event) return RunTexts.NothingToAnswer;
            if (run.Stop.Outcome != null) return RunTexts.Answered;
            if (option != 0 && option != 1) return RunTexts.NoOption;
            // An event's option (H4): what it costs, from the squad's packs.
            if (run.Stop.Event != null && Content.RunEvents.TryGetValue(run.Stop.Event, out var def) && option < def.Options.Count)
            {
                var o = def.Options[option];
                var have = Standing(c).Sum((s) => Gears.Supplies(s.Kit!.Pack));
                if (o.Supplies < 0 && have < -o.Supplies) return Lore.RunEventShort(-o.Supplies - have);
            }
            return null;
        }

        private static void AnswerStop(CampaignState c, int option, Rng rng)
        {
            Event(c, option, rng);
        }

        /// <summary>
        /// An event's option (H4, Lore's rules): what it costs, paid from the squad's packs; the health it takes, from one
        /// at random of the squad who has more than that (never killing) or from everyone (never below 1); the health it
        /// gives; what it gives, into the packs (the one it fell to first), and lost where no pack has room; the modifier it
        /// leaves for the next fight; who joins. StopState.result says how it went.
        /// </summary>
        private static void Event(CampaignState c, int option, Rng rng)
        {
            var run = c.Run!;
            var stop = run.Stop!;
            var def = Content.RunEvents[stop.Event!];
            var o = def.Options[option];
            var squad = Standing(c);
            var result = new EventResult { Event = def.Id, Option = option };
            if (o.Supplies < 0) Gears.PaySupplies(squad.Select((s) => s.Kit!.Pack), -o.Supplies);
            CampaignSoldier? hit = null;
            if (o.HurtOne > 0)
            {
                var able = squad.Where((s) => HpOf(c, s) > o.HurtOne).ToList();
                if (able.Count > 0)
                {
                    hit = able[RunRng(run, 0x0e).Int(able.Count)];
                    run.Hp[hit.Id] = HpOf(c, hit) - o.HurtOne;
                    result.Soldier = hit.Id;
                    result.SoldierName = hit.Name;
                }
            }
            foreach (var s in squad)
            {
                if (o.HurtAll > 0) run.Hp[s.Id] = Math.Max(1, HpOf(c, s) - o.HurtAll);
                if (o.HealAll > 0) run.Hp[s.Id] = Math.Min(MaxHp(s), HpOf(c, s) + o.HealAll);
            }
            var lost = false;
            // Into the packs: the one it fell to first, then the squad in order; what none takes whole, onto equal piles; the rest lost.
            void Give(string kind, string? type, int count)
            {
                var order = (hit != null ? new[] { hit } : new CampaignSoldier[0]).Concat(squad.Where((s) => s != hit)).ToList();
                var pile = Content.PileOf(kind, type);
                for (var left = count; left > 0; left -= pile)
                {
                    var g = Gears.NewGear(c, kind, type, Math.Min(pile, left));
                    var into = order.FirstOrDefault((s) => Gears.CanStow(s.Kit!.Pack, g));
                    if (into != null)
                    {
                        Gears.Stow(into.Kit!.Pack, g);
                        continue;
                    }
                    foreach (var p in order.SelectMany((s) => s.Kit!.Pack.Items)) if (Gears.SamePile(p.Gear, g)) Gears.Merge(p.Gear, g);
                    if (g.Count > 0) lost = true;
                }
            }
            if (o.Supplies > 0) Give(GearKinds.Supplies, null, o.Supplies);
            if (o.GearKind != null && o.GearCount > 0) Give(o.GearKind, o.GearType, o.GearCount);
            if (o.Modifier != null) run.Pending = o.Modifier;
            // Someone out there (N2: «herido», «radio»): a fight of the next row becomes a Rescate, the survivor alone.
            if (o.Rescue) result.Rescue = MarkRescue(run)?.Id;
            result.Lost = lost ? true : (bool?)null;
            stop.Result = result;
            stop.Outcome = $"{def.Id}.{option}";
            if (Lore.RunEvents.TryGetValue(def.Id, out var told)) Campaign.RunsLog(c, told.Options[option].Result(result.SoldierName, result.SurvivorName) + (lost ? $" {Lore.RunEventLost}" : ""));
        }

        /// <summary>
        /// A fight the stop leads to made a Rescate without Cuervos (N2, an event's RunEventOption.Rescue): a normal one if
        /// there is, else another special; none if one already is, or nothing it leads to is a fight. Which.
        /// </summary>
        private static RunNode? MarkRescue(RunState run)
        {
            var next = RunMap.Node(run, run.At)?.Next.Select((id) => RunMap.Node(run, id)).Where((n) => n != null).Select((n) => n!).ToList() ?? new List<RunNode>();
            if (next.Any((n) => n.Kind == NodeKinds.Rescue)) return null;
            var fights = next.Where((n) => RunMap.IsFight(n.Kind) && n.Kind != NodeKinds.Boss).ToList();
            var n = fights.FirstOrDefault((x) => x.Kind == NodeKinds.Normal) ?? fights.FirstOrDefault();
            if (n == null) return null;
            n.Kind = NodeKinds.Rescue;
            n.Guarded = null;
            return n;
        }

        /// <summary>Why the squad can't go on from the stop (LeaveStopCommand): an event is answered first.</summary>
        public static string? LeaveStopRefusal(CampaignState c)
        {
            var run = c.Run;
            if (run == null || run.Phase != RunPhases.Stop || run.Stop == null) return RunTexts.NotAtStop;
            var kind = RunMap.Node(run, run.At)?.Kind;
            return kind == NodeKinds.Event && run.Stop.Outcome == null ? RunTexts.AnswerFirst : null;
        }

        /// <summary>On to the map: alone at once; with the partner here, once both have asked (as the launch of before).</summary>
        private static void LeaveStop(CampaignState c, int slot, IReadOnlyList<int> connected, bool ready)
        {
            var run = c.Run!;
            var partnerHere = connected.Any((s) => s != slot) && c.Players.Count > 1;
            if (partnerHere)
            {
                if (run.Stop!.Leaving.Length < 2) run.Stop.Leaving = new bool[2];
                run.Stop.Leaving[slot] = ready;
                if (!ready || !connected.All((s) => run.Stop.Leaving[s])) return;
            }
            else if (!ready) return;
            run.Phase = RunPhases.Choose;
            run.Stop = null;
            AfterNode(c);
        }
    }
}
