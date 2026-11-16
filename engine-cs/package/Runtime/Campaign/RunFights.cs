#nullable enable
// A run's node fought (H3, .dev-snapshots/gameplay/runs/H0-DISENO.md §6; new in C#): the mission of the node the squad
// is at, made the way the operations of before were (Setup.CreateMission, their groups by difficulty), each soldier out
// with its kit, its pack and the health it has left; and the fight folded back into the run: kits and health home, the
// fallen gone for good, the bars learnt, a special's reward, and the run going on, ended, or the chapter done.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Runs
    {
        /// <summary>The cell of the board that gives a run's pack a row more («Todo cabe», «care»).</summary>
        public const string CareCell = "care";
        /// <summary>A special won with its objective: its reward, in § (Élite, later, 80–120).</summary>
        public const int RewardMin = 40;
        public const int RewardMax = 80;

        /// <summary>A soldier's pack rows: the upgrades', and one more with «Todo cabe».</summary>
        public static int PackRowsOf(CampaignState c, CampaignSoldier s)
        {
            return PackRows(c) + (s.Board.Contains(CareCell) ? 1 : 0);
        }

        /// <summary>Whose a soldier is in the fight: the one it was handed to in the run (RunState.hands), or its owner.</summary>
        public static int OwnerOf(CampaignState c, CampaignSoldier s)
        {
            return c.Run?.Hands != null && c.Run.Hands.TryGetValue(s.Id, out var hand) ? hand : s.Owner;
        }

        /// <summary>The mission a node is: a normal one, a Saqueo (no objective, out on La Lata); the boss, for now, a Limpieza (H6).</summary>
        public static string MissionKindOf(string node)
        {
            switch (node)
            {
                case NodeKinds.Normal:
                    return MissionKinds.Scavenge;
                case NodeKinds.Elimination:
                case NodeKinds.Recovery:
                case NodeKinds.Sabotage:
                case NodeKinds.Hack:
                case NodeKinds.Rescue:
                    return node;
                default:
                    // TODO(H5): hold, crossing, stealth and elite; TODO(H6): the boss. Meanwhile, a Limpieza.
                    return MissionKinds.Elimination;
            }
        }

        /// <summary>The boss's for now (H6 brings the real one): a heavy group on top of the Limpieza's.</summary>
        private static List<string> BossPod()
        {
            return new List<string> { TemplateIds.Mec, TemplateIds.Xenoid, TemplateIds.Bloater, TemplateIds.Zombie, TemplateIds.Zombie };
        }

        /// <summary>La Lata as big as the seats the squad has (Campaign.VehicleSeats: 4, 6 or 8).</summary>
        private static int VehicleOf(CampaignState c)
        {
            var seats = SeatsOf(c);
            var tier = Campaign.VehicleSeats.ToList().FindIndex((n) => n >= seats);
            return tier >= 0 ? tier : Campaign.VehicleSeats.Count - 1;
        }

        /// <summary>
        /// A run's soldier going into a fight (Setup.SquadMember): its weapons from its kit (with their mods, no wear), the
        /// belt's things as uses, the vest's health and armour, its pack and its magazines, the health it has left.
        /// </summary>
        public static SquadMember SquadMemberOf(CampaignState c, CampaignSoldier s)
        {
            var kit = s.Kit ?? new Kit();
            var arms = new Arms
            {
                Primary = kit.Primary?.Type,
                PrimaryId = kit.Primary?.Id,
                Mods = kit.Primary?.Mods != null && kit.Primary.Mods.Count > 0 ? new List<string>(kit.Primary.Mods) : null,
                // The knife is the blade everyone fights with when there is no other.
                Melee = kit.Melee == null || kit.Melee.Type == FoundWeaponIds.Knife ? null : kit.Melee.Type,
                MeleeId = kit.Melee?.Id,
                Pistol = kit.Pistol != null,
                PistolId = kit.Pistol?.Id,
            };
            var belt = kit.Belt.Where((g) => g?.Type != null).SelectMany((g) => Enumerable.Repeat(g!.Type!, g.Count)).ToList();
            var stats = Bars.FighterStatsOf(s.Skills, s.Growth, s.Board, belt, arms);
            var traits = s.Traits.Select((t) => Quests.Traits[t]).ToList();
            var vest = kit.Vest?.Type != null && Content.Vests.TryGetValue(kit.Vest.Type, out var def) ? def : null;
            var max = stats.MaxHp + (vest?.Hp ?? 0);
            var hp = c.Run != null && c.Run.Hp.TryGetValue(s.Id, out var left) ? Math.Max(1, Math.Min(max, left)) : max;
            return new SquadMember
            {
                CampaignId = s.Id,
                Name = s.Name,
                Nickname = s.Nickname,
                Appearance = s.Appearance,
                WeaponTier = Progression.WeaponTier(),
                ArmorTier = 1,
                WeaponName = RunPack.WeaponName(kit),
                Owner = OwnerOf(c, s),
                Template = stats.Template,
                Rank = stats.Rank,
                Perks = stats.Perks,
                Mods = Content.AddMods(stats.Mods, new UnitMods { Aim = traits.Sum((t) => t.Aim) }),
                MaxHp = max,
                Hp = hp,
                Charges = stats.Charges,
                Will = stats.Will + traits.Sum((t) => t.Will),
                Armor = stats.Armor + (vest?.Armor ?? 0),
                Gear = belt.Distinct().ToList(),
                Arms = arms,
                Mags = kit.Pack.Items.Where((p) => p.Gear.Kind == GearKinds.Mags).Sum((p) => p.Gear.Count),
                Lines = Bars.MissionLinesOf(s.Skills, s.Growth),
                Pack = kit.Pack,
                Kit = Gears.ClonePlaces(kit),
            };
        }

        // ---------------------------------------------------------------- launch

        /// <summary>
        /// The mission of the node the squad is at, fighting (the room's LaunchMission for a run campaign): its map from its
        /// site and seed, its kind, its groups by its difficulty as an operation of before had them, its modifier (night,
        /// storm, the horde awake), and the squad with its kits. Null when the run isn't at a fight.
        /// </summary>
        public static (GameState State, string Name)? Launch(CampaignState c, Rng rng)
        {
            var run = c.Run;
            if (run == null || run.Phase != RunPhases.Fight) return null;
            var node = RunMap.Node(run, run.At);
            var map = node != null ? RunMap.MapOf(node) : null;
            if (node == null || map == null) return null;
            var kind = MissionKindOf(node.Kind);
            var difficulty = Math.Max(1, Math.Min(4, node.Difficulty));
            var living = kind == MissionKinds.Rescue;
            var pods = Setup.MissionPods(difficulty, kind, living);
            if (node.Kind == NodeKinds.Boss) pods.Add(BossPod());
            var squad = run.Squad.Select((id) => c.Soldiers.FirstOrDefault((s) => s.Id == id)).Where((s) => s != null && s.Alive && s.Kit != null).Select((s) => SquadMemberOf(c, s!)).ToList();
            var seed = Math.Abs(node.Seed ?? 0);
            var alarm = kind == MissionKinds.Sabotage ? Alarms.Factory : null;
            var name = RunTexts.FightName(node);
            var state = Setup.CreateMission(new MissionOptions
            {
                Map = map,
                Slots = c.Players.ToList(),
                Rng = rng,
                Kind = kind,
                Squad = squad,
                Pods = Setup.PodsFor(map, pods),
                Difficulty = difficulty,
                Living = living ? true : (bool?)null,
                CampaignMission = new CampaignMission { Id = node.Id, Name = name },
                VipName = kind == MissionKinds.Rescue ? Setup.VipNames[seed % Setup.VipNames.Count] : null,
                Alarm = alarm,
                ObjectiveName = alarm != null ? Lore.AlarmNames[alarm] : null,
                // The node's own modifier, and the one an event left for it (H4).
                Night = node.Modifier == Modifiers.Night || node.Added == Modifiers.Night ? true : (bool?)null,
                Weather = node.Modifier == Modifiers.Storm || node.Added == Modifiers.Storm ? Weathers.Storm : null,
                Awake = node.Modifier == Modifiers.Awake || node.Added == Modifiers.Awake ? true : (bool?)null,
                // Pistols are counted: searches turn them up too.
                Pistols = true,
                Vehicle = VehicleOf(c),
            });
            state.Run = new RunFight { Node = node.Id, NodeKind = node.Kind, Row = node.Row, Modifier = node.Modifier, Added = node.Added, FirstGear = c.NextId, NextGear = c.NextId };
            return (state, name);
        }

        // ------------------------------------------------------------ the result

        /// <summary>
        /// The one a Rescate brought out, a soldier now (Runs.NewSoldier): by their name, of whoever has fewest soldiers;
        /// into the squad with the health they had if there is a seat, else waiting at the base. Null with no bed for them.
        /// </summary>
        private static CampaignSoldier? Rescued(CampaignState c, Unit vip, Rng rng)
        {
            var owner = Campaign.RunsNeediest(c);
            if (c.Soldiers.Count((s) => s.Owner == owner && s.Alive) >= Campaign.SoldierCap(c)) return null;
            var s = NewSoldier(c, owner, rng);
            s.Name = vip.Name;
            s.Appearance = Appearances.DefaultAppearance(owner, null, Campaign.SexOfPerson(vip.Name) ?? s.Appearance.Sex, s.Id, s.Appearance.Wardrobe);
            var run = c.Run!;
            if (run.Squad.Count < SeatsOf(c))
            {
                run.Squad.Add(s.Id);
                run.Hp[s.Id] = Math.Max(1, Math.Min(MaxHp(s), vip.Hp));
            }
            Campaign.RunsLog(c, RunTexts.SurvivorJoins(s.Name), LogTones.Good);
            return s;
        }

        /// <summary>The number of a gear's id ("g17": 17), or 0.</summary>
        private static int GearNumber(string id)
        {
            return id.Length > 1 && int.TryParse(id.Substring(1), out var n) ? n : 0;
        }

        /// <summary>Every gear a kit and its pack hold.</summary>
        private static IEnumerable<Gear> AllOf(Kit k)
        {
            foreach (var g in new[] { k.Primary, k.Melee, k.Pistol, k.Vest }.Concat(k.Belt)) if (g != null) yield return g;
            foreach (var p in k.Pack.Items) yield return p.Gear;
        }

        /// <summary>What a soldier brings home from the unit it was: its kit's places, the belt with what is left of it, its pack.</summary>
        private static Kit KitBack(Unit u, CampaignSoldier s)
        {
            // Copies: the mission's state stays the room's.
            var places = Gears.ClonePlaces(u.Kit ?? new KitPlaces());
            var belt = RunPack.BeltLeft(u);
            return new Kit
            {
                Primary = places.Primary,
                Melee = places.Melee,
                Pistol = places.Pistol,
                Vest = places.Vest,
                Belt = belt,
                Pack = Gears.CloneGrid(u.Pack ?? s.Kit?.Pack ?? new GearGrid { W = PackW, H = PackH }),
            };
        }

        /// <summary>
        /// A run's fight folded back into the run (the room's ApplyMissionResult for a run campaign), once: what each
        /// soldier comes back with and its health; the fallen dead for good, their gear lost unless another took it up; the
        /// bars, as missions of before taught them; a special won, its reward into the packs; and then the run: back to the
        /// map, the chapter done with the boss dead, or over with the squad fallen. Its report is LastReport.
        /// </summary>
        public static MissionReport ApplyFight(CampaignState c, GameState mission, Rng rng)
        {
            var run = c.Run;
            var fight = mission.Run;
            var victory = mission.Outcome == Outcomes.Victory;
            var node = run != null ? RunMap.Node(run, run.At) : null;
            var report = new MissionReport
            {
                Day = c.Day,
                MissionName = mission.CampaignMission?.Name ?? "",
                Victory = victory,
                Reason = mission.OutcomeReason ?? EndReasons.Wiped,
                Squad = new List<ReportSoldier>(),
                Night = mission.Mission.Night == true ? true : (bool?)null,
                Run = new RunReport { Node = node?.Id ?? fight?.Node ?? "", NodeKind = node?.Kind ?? fight?.NodeKind ?? "" },
            };
            c.LastReport = report;
            if (run == null || node == null || fight == null || run.Phase != RunPhases.Fight) return report;
            // The gear counter goes on from where the fight left it.
            c.NextId = Math.Max(c.NextId, fight.NextGear);
            bool Fell(Unit u) => (!u.Alive && !u.Evacuated && u.Hp <= 0) || u.Lost == true;
            var fought = 0;
            var standing = 0;
            foreach (var u in mission.Units)
            {
                var s = !string.IsNullOrEmpty(u.CampaignId) ? c.Soldiers.FirstOrDefault((x) => x.Id == u.CampaignId) : null;
                if (s == null) continue;
                fought++;
                s.Missions++;
                s.Kills += u.Kills;
                var entry = new ReportSoldier { Id = s.Id, Name = s.Name, Owner = s.Owner, Status = ReportStatuses.Ok, WoundedDays = 0, Kills = u.Kills };
                report.Squad.Add(entry);
                if (Fell(u))
                {
                    s.Alive = false;
                    s.FellDay = c.Day;
                    entry.Status = u.Lost == true ? ReportStatuses.Lost : ReportStatuses.Dead;
                    // What it carried: left on the floor where it fell (nobody took it up), or with it, left behind.
                    var lost = u.Lost == true ? (u.Kit != null ? new[] { u.Kit.Primary, u.Kit.Melee, u.Kit.Pistol, u.Kit.Vest }.Concat(u.Kit.Belt).Where((g) => g != null).Select((g) => g!).Concat(u.Pack?.Items.Select((p) => p.Gear) ?? new Gear[0]).ToList() : new List<Gear>())
                        : (mission.FloorGear ?? new List<DroppedGear>()).Where((d) => d.From == u.Id).Select((d) => d.Gear).ToList();
                    report.Run.Fallen.Add(new RunFallen { Id = s.Id, Name = s.Name, Lost = lost.Select(Gears.CloneGear).ToList() });
                    s.Kit = null;
                    run.Hp.Remove(s.Id);
                    run.Hands?.Remove(s.Id);
                    Campaign.RunsLog(c, $"{s.Name} ha caído en combate.", LogTones.Bad);
                    continue;
                }
                standing++;
                s.Kit = KitBack(u, s);
                var hp = Math.Max(1, Math.Min(MaxHp(s), u.Hp));
                run.Hp[s.Id] = hp;
                if (hp < MaxHp(s)) entry.Status = ReportStatuses.Wounded;
                // What it did teaches it (docs/FASE10.md §3), as on the missions of before.
                var taught = Bars.Learn(s.Skills, s.Growth, u.Deeds, rng, 0, u.Lines);
                foreach (var bar in taught.Bars) Campaign.RunsLog(c, Lore.BarLine(s.Name, bar.Skill, bar.Growth, bar.From ?? 0), LogTones.Good);
                entry.Learned = taught.Gained;
                var deeds = new Deeds();
                foreach (var d in u.Deeds ?? new Deeds()) deeds[d.Key] = d.Value;
                entry.Deeds = deeds;
                entry.Bars = taught.Bars;
                s.Skills = taught.Skills;
                s.Growth = taught.Growth;
            }
            run.Squad = run.Squad.Where((id) => c.Soldiers.Any((s) => s.Id == id && s.Alive)).ToList();
            c.Squad = c.Squad.Where((id) => c.Soldiers.Any((s) => s.Id == id && s.Alive)).ToList();
            var home = run.Squad.Select((id) => c.Soldiers.First((s) => s.Id == id)).Where((s) => s.Kit != null).ToList();
            report.Run.Found = home.SelectMany((s) => AllOf(s.Kit!)).Where((g) => GearNumber(g.Id) > fight.FirstGear).Select(Gears.CloneGear).ToList();

            // The squad fell: the run is over, and what they carried with it.
            if (fought > 0 && standing == 0)
            {
                report.Run.Ended = RunEnds.Wiped;
                EndRun(c, rng, RunTexts.Wiped);
                return report;
            }
            // A special won with its objective: its reward, into the packs while there is room.
            if (victory && RunMap.Specials.Concat(RunMap.NewSpecials).Contains(node.Kind))
            {
                var reward = rng.Range(RewardMin, RewardMax);
                var left = 0;
                foreach (var pile in Enumerable.Range(0, (reward + 99) / 100).Select((i) => Gears.NewGear(c, GearKinds.Supplies, null, Math.Min(100, reward - i * 100))).ToList())
                {
                    var into = home.FirstOrDefault((s) => Gears.CanStow(s.Kit!.Pack, pile));
                    if (into != null)
                    {
                        Gears.Stow(into.Kit!.Pack, pile);
                        continue;
                    }
                    // What no pack takes whole: as much as the piles there take; the rest is lost.
                    foreach (var p in home.SelectMany((s) => s.Kit!.Pack.Items)) if (Gears.SamePile(p.Gear, pile)) Gears.Merge(p.Gear, pile);
                    left += pile.Count;
                }
                report.Run.Reward = reward;
                report.Run.RewardLost = left > 0 ? left : (int?)null;
                Campaign.RunsLog(c, RunTexts.Reward(reward, left), LogTones.Good);
            }
            // A Rescate won with the one held out (H4): they join, with a knife and the health they had.
            if (victory && node.Kind == NodeKinds.Rescue)
            {
                var vip = mission.Units.FirstOrDefault((u) => u.Id == mission.Mission.ObjectiveUnit && u.Template == TemplateIds.Vip);
                if (vip != null && vip.Evacuated) report.Run.Joined = Rescued(c, vip, rng)?.Id;
            }
            if (node.Kind == NodeKinds.Boss)
            {
                if (victory)
                {
                    report.Run.Ended = RunEnds.Boss;
                    NodeCleared(c, rng);
                }
                else
                {
                    report.Run.Ended = RunEnds.Returned;
                    EndRun(c, rng);
                }
                return report;
            }
            NodeCleared(c, rng);
            if (c.Run != null) report.Run.Hp = new Dictionary<string, int>(c.Run.Hp);
            return report;
        }
    }
}
