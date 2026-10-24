using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/skills.test.ts: Fase 10, barras de experiencia y tablero hexagonal (docs/FASE10.md). Every
    /// test of it; `client toEqual s` is Step2Json.StateDiff.
    /// </summary>
    public class SkillsTests
    {
        /// <summary>Lines with these bars filled, and nothing into the next.</summary>
        private static Skills BarsFilled(params (string Id, int N)[] filled)
        {
            var out_ = Bars.NoSkills();
            foreach (var (id, n) in filled) out_[id] = Bars.BarsCost(n);
            return out_;
        }

        /// <summary>`{ ...NO_SKILLS, … }` (or NO_GROWTH): every line, these with their numbers.</summary>
        private static Dictionary<string, int> AllLines(params (string Id, int N)[] lines)
        {
            var out_ = SkillIds.All.ToDictionary((id) => id, (id) => 0);
            foreach (var (id, n) in lines) out_[id] = n;
            return out_;
        }

        private static Growth GrowthOf(params (string Id, int N)[] lines)
        {
            var out_ = Bars.NoGrowth();
            foreach (var (id, n) in lines) out_[id] = n;
            return out_;
        }

        private static Deeds DeedsOf(params (string Id, int N)[] deeds)
        {
            var out_ = new Deeds();
            foreach (var (id, n) in deeds) out_[id] = n;
            return out_;
        }

        /// <summary>toEqual on a record of numbers: the same keys, the same numbers, in any order.</summary>
        private static void SameMap(IEnumerable<KeyValuePair<string, int>> expected, IEnumerable<KeyValuePair<string, int>>? actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(expected.OrderBy((e) => e.Key, StringComparer.Ordinal).Select((e) => $"{e.Key}={e.Value}"), actual!.OrderBy((e) => e.Key, StringComparer.Ordinal).Select((e) => $"{e.Key}={e.Value}"));
        }

        private static (int, int, int) Progress(BarProgress p)
        {
            return (p.Filled, p.Into, p.Need);
        }

        // ------------------------------------------------------------ the bars

        [Fact(DisplayName = "the bars › take 10, 15, 20… and start again when full")]
        public void TakeTenFifteenTwenty()
        {
            Assert.Equal(new[] { 10, 15, 20, 25 }, new[] { 0, 1, 2, 3 }.Select(Bars.BarCost));
            Assert.Equal(new[] { 0, 10, 25, 45 }, new[] { 0, 1, 2, 3 }.Select(Bars.BarsCost));
            Assert.Equal((0, 0, 10), Progress(Bars.BarsOf(0)));
            Assert.Equal((0, 9, 10), Progress(Bars.BarsOf(9)));
            Assert.Equal((1, 0, 15), Progress(Bars.BarsOf(10)));
            Assert.Equal((2, 7, 20), Progress(Bars.BarsOf(32)));
        }

        [Fact(DisplayName = "the bars › deeds turn into experience, line by line, and a bonus adds its share rounded up")]
        public void DeedsTurnIntoExperience()
        {
            var deeds = DeedsOf((DeedIds.Shot, 3), (DeedIds.Hit, 2), (DeedIds.GunKill, 1), (DeedIds.Swing, 1), (DeedIds.Blow, 1), (DeedIds.Throw, 2), (DeedIds.ThrowHit, 1), (DeedIds.HealOther, 1), (DeedIds.HealSelf, 1), (DeedIds.Search, 2), (DeedIds.Fix, 1), (DeedIds.Hunker, 2), (DeedIds.Run, 3));
            SameMap(AllLines((SkillIds.Shooting, 6), (SkillIds.Melee, 2), (SkillIds.Throwing, 3), (SkillIds.Medicine, 3), (SkillIds.Searching, 7), (SkillIds.Fitness, 5)), Bars.LearnedFrom(deeds));
            var bonus = Bars.LearnedFrom(DeedsOf((DeedIds.Shot, 9), (DeedIds.Hit, 4)), 0.2);
            Assert.Equal(16, bonus[SkillIds.Shooting]);
            Assert.Equal(0, bonus[SkillIds.Melee]);
            SameMap(Bars.NoSkills(), Bars.LearnedFrom(null));
        }

        [Fact(DisplayName = "the bars › a bar that fills adds 5 to 10 % to its line and a point; two at once, two of each")]
        public void ABarThatFillsAddsGrowthAndAPoint()
        {
            var start = Bars.NoSkills();
            start[SkillIds.Shooting] = 8;
            var r = Bars.Learn(start, Bars.NoGrowth(), DeedsOf((DeedIds.Shot, 10), (DeedIds.Hit, 9), (DeedIds.GunKill, 3), (DeedIds.Hunker, 2)), new Rng(4));
            // 8 + 22 = 30: the first bar (10) and the second (25).
            SameMap(AllLines((SkillIds.Shooting, 30), (SkillIds.Fitness, 2)), r.Skills);
            SameMap(AllLines((SkillIds.Shooting, 22), (SkillIds.Fitness, 2)), r.Gained);
            Assert.Single(r.Bars);
            Assert.Equal(SkillIds.Shooting, r.Bars[0].Skill);
            Assert.Equal(2, r.Bars[0].Filled);
            Assert.Equal(2, r.Bars[0].Growth.Count);
            foreach (var g in r.Bars[0].Growth)
            {
                Assert.True(g >= 5, $"{g} >= 5");
                Assert.True(g <= 10, $"{g} <= 10");
            }
            Assert.Equal(r.Bars[0].Growth[0] + r.Bars[0].Growth[1], r.Growth[SkillIds.Shooting]);
            Assert.Equal(0, r.Growth[SkillIds.Fitness]);
            Assert.Equal(2, r.Points);
        }

        [Fact(DisplayName = "the bars › what the lines add: a share of the base, rounded when it is used")]
        public void WhatTheLinesAdd()
        {
            var st = Bars.FighterStatsOf(Bars.NoSkills(), GrowthOf((SkillIds.Shooting, 10), (SkillIds.Melee, 20), (SkillIds.Throwing, 10), (SkillIds.Medicine, 13), (SkillIds.Searching, 10), (SkillIds.Fitness, 23)), new string[0]);
            // 60 → 66 and 72; a throw of 10 → 11; the medkit's 4 → 5 (4.52); 55 → 61 (60.5); health 5 → 6 (6.15).
            Assert.Equal(6, st.Mods.GunAim);
            Assert.Equal(12, st.Mods.MeleeAim);
            Assert.Equal(1, st.Mods.GrenadeRange);
            Assert.Equal(1, st.Mods.Heal);
            Assert.Equal(6, st.Mods.Hack);
            Assert.Equal(6, st.MaxHp);
            Assert.Equal(5, Bars.FighterStatsOf(Bars.NoSkills(), Bars.NoGrowth(), new string[0]).MaxHp);
        }

        // ------------------------------------------------------------ veterancy (Arturo, 16/10)

        [Fact(DisplayName = "veterancy (Arturo, 16/10) › a rank for every 5 points spent on the board, with its names")]
        public void ARankForEveryFivePoints()
        {
            Assert.Equal(6, Bars.TotalBars(BarsFilled((SkillIds.Shooting, 3), (SkillIds.Melee, 2), (SkillIds.Fitness, 1))));
            Assert.Equal(new[]
            {
                "Novato",
                "Novato",
                "Fogueado",
                "Fogueado",
                "Curtido",
                "Curtido",
                "Veterano",
                "Veterano",
                "Superviviente",
                "Superviviente",
            }, new[] { 0, 4, 5, 9, 10, 14, 15, 19, 20, 40 }.Select((v) => Bars.Veterancy[Bars.VeterancyRank(v)].Name));
        }

        [Fact(DisplayName = "veterancy (Arturo, 16/10) › each rank gives 10 of will, and a Veterano the third action; bars not spent count for nothing")]
        public void EachRankGivesWill()
        {
            Assert.Equal(new[] { 40, 50, 80 }, new[] { 0, 1, 4 }.Select(Bars.VeteranWill));
            var skills = BarsFilled((SkillIds.Shooting, 5), (SkillIds.Melee, 5), (SkillIds.Fitness, 5), (SkillIds.Medicine, 5));
            Assert.Equal(0, Bars.FighterStatsOf(skills, Bars.NoGrowth(), new string[0]).Rank);
            Assert.Equal(40, Bars.FighterStatsOf(skills, Bars.NoGrowth(), new string[0]).Will);
            var board = Bars.RandomBoard(skills, new Rng(2));
            Assert.Equal(20, board.Count);
            var fourteen = Bars.FighterStatsOf(skills, Bars.NoGrowth(), board.Take(14).ToList());
            Assert.Equal((2, 60), (fourteen.Rank, fourteen.Will));
            var fifteen = Bars.FighterStatsOf(skills, Bars.NoGrowth(), board.Take(15).ToList());
            Assert.Equal((3, 70), (fifteen.Rank, fifteen.Will));
            Assert.Equal(3, Content.ApPerTurnOf(new Unit { Team = Teams.Xcom, Rank = 3 }));
        }

        // ------------------------------------------------------------ newcomers

        [Fact(DisplayName = "newcomers › a recruit has a bar in one or two lines, not always the same, with its growth")]
        public void ARecruitHasABarInOneOrTwoLines()
        {
            var seen = new HashSet<string>();
            for (var seed = 1; seed <= 30; seed++)
            {
                var lines = Bars.RecruitSkills(new Rng(seed));
                var full = SkillIds.All.Where((id) => Bars.BarsOf(lines.Skills[id]).Filled == 1).ToList();
                Assert.True(full.Count >= 1, $"seed {seed}: {full.Count} >= 1");
                Assert.True(full.Count <= 2, $"seed {seed}: {full.Count} <= 2");
                Assert.Equal(full.Count, Bars.TotalBars(lines.Skills));
                foreach (var id in SkillIds.All) Assert.Equal(full.Contains(id), lines.Growth[id] > 0);
                seen.Add(string.Join(",", full));
            }
            Assert.True(seen.Count > 4, $"{seen.Count} > 4");
        }

        [Fact(DisplayName = "newcomers › a skirmish veteran has two to five bars in every line and every point spent")]
        public void ASkirmishVeteranHasTwoToFiveBars()
        {
            for (var seed = 1; seed <= 10; seed++)
            {
                var lines = Bars.VeteranSkills(new Rng(seed));
                var board = lines.Board!;
                foreach (var id in SkillIds.All) Assert.True(Bars.BarsOf(lines.Skills[id]).Filled >= 2, $"seed {seed}, {id}");
                Assert.Equal(Bars.TotalBars(lines.Skills), board.Count);
                Assert.Equal(0, Bars.PointsLeft(lines.Skills, board));
                // Bought one by one, each beside the ones before.
                for (var i = 0; i < board.Count; i++) Assert.Null(Bars.UnlockBlocker(lines.Skills, board.Take(i).ToList(), board[i]));
            }
        }

        // ------------------------------------------------------------ the hex board (docs/FASE10.md §6)

        private static int HexDistance(int q, int r)
        {
            return (Math.Abs(q) + Math.Abs(r) + Math.Abs(q + r)) / 2;
        }

        [Fact(DisplayName = "the hex board (docs/FASE10.md §6) › 169 cells: the core and seven rings, six sectors of 28")]
        public void OneHundredSixtyNineCells()
        {
            Assert.Equal(169, Board.Cells.Count);
            Assert.Equal(169, Board.Cells.Select((c) => c.Id).Distinct().Count());
            Assert.Equal(169, Board.Cells.Select((c) => $"{c.Q},{c.R}").Distinct().Count());
            Assert.Equal(169, Board.Cells.Select((c) => c.Name).Distinct().Count());
            for (var k = 1; k <= 7; k++) Assert.Equal(6 * k, Board.Cells.Count((c) => c.Ring == k));
            for (var i = 0; i < 6; i++)
            {
                var sector = Board.Cells.Where((c) => c.Sector == i).ToList();
                Assert.Equal(28, sector.Count);
                foreach (var c in sector) Assert.Equal(Board.SectorLines[i], c.Line);
            }
            foreach (var c in Board.Cells) Assert.True(HexDistance(c.Q, c.R) == c.Ring, c.Id);
        }

        [Fact(DisplayName = "the hex board (docs/FASE10.md §6) › the core touches one cell of every sector; every cell has its neighbours on the board")]
        public void TheCoreTouchesEverySector()
        {
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, Board.BoardNeighbors(Board.Core).Select((id) => Board.BoardCells[id].Sector).OrderBy((x) => x));
            foreach (var c in Board.Cells)
            {
                var n = Board.BoardNeighbors(c.Id).Count;
                Assert.True(n == (c.Ring == 7 ? (n == 3 ? 3 : 4) : 6), c.Id);
            }
        }

        [Fact(DisplayName = "the hex board (docs/FASE10.md §6) › every active gives its ability, and every skill says what it does")]
        public void EveryActiveGivesItsAbility()
        {
            foreach (var c in Board.Cells)
            {
                Assert.True(c.Effect.Length > 5, c.Id);
                if (c.Kind == CellKinds.Active) Assert.True(Board.PerkAbility[c.Perk!] == c.Ability, c.Id);
            }
        }

        [Fact(DisplayName = "the hex board (docs/FASE10.md §6) › a skill is bought beside one already had, with a point, and nothing else")]
        public void ASkillIsBoughtBesideOneAlreadyHad()
        {
            var first = Board.Cells.First((c) => c.Ring == 1 && c.Line == SkillIds.Shooting);
            var second = Board.Cells.First((c) => c.Ring == 2 && c.Line == SkillIds.Shooting && Board.BoardNeighbors(c.Id).Contains(first.Id));
            var far = Board.Cells.First((c) => c.Ring == 3 && c.Line == SkillIds.Shooting);
            var one = BarsFilled((SkillIds.Melee, 1));
            var none = new string[0];
            Assert.Equal("open", Bars.CellState(none, first.Id));
            Assert.Equal("locked", Bars.CellState(none, far.Id));
            Assert.Equal("unlocked", Bars.CellState(none, Board.Core));
            Assert.Null(Bars.UnlockBlocker(one, none, first.Id));
            Assert.Contains("al lado", Bars.UnlockBlocker(one, none, far.Id));
            // A point from any line opens any cell beside: no bars of its own line asked (Arturo, 16/10).
            Assert.Null(Bars.UnlockBlocker(BarsFilled((SkillIds.Melee, 2)), new[] { first.Id }, second.Id));
            Assert.Contains("No le quedan puntos", Bars.UnlockBlocker(one, new[] { first.Id }, second.Id));
            Assert.Equal("Ya la tiene.", Bars.UnlockBlocker(one, new[] { first.Id }, first.Id));
            Assert.Equal("Ya la tiene.", Bars.UnlockBlocker(one, none, Board.Core));
            Assert.Equal("Esa casilla no existe.", Bars.UnlockBlocker(one, none, "nothing"));
            Assert.Equal(2, Bars.PointsLeft(BarsFilled((SkillIds.Shooting, 2), (SkillIds.Fitness, 1)), new[] { first.Id }));
        }

        [Fact(DisplayName = "the hex board (docs/FASE10.md §6) › what the cells give: numbers, health, armour, rules, abilities, and spare uses only of what is carried")]
        public void WhatTheCellsGive()
        {
            var st = Bars.FighterStatsOf(Bars.NoSkills(), Bars.NoGrowth(), new[] { "steadyHand", "stamina", "vest", "implacable", "aimedShot", "bandolier", "bigKit", "restoration" });
            Assert.Equal(3, st.Mods.GunAim);
            Assert.Equal(6, st.MaxHp);
            Assert.Equal(1, st.Armor);
            Assert.Equal(new[] { PerkIds.Implacable, PerkIds.AimedShot, PerkIds.Restoration }, st.Perks);
            SameMap(new Dictionary<string, int> { [ChargeIds.Restoration] = 1 }, st.Charges);
            var carrying = Bars.FighterStatsOf(Bars.NoSkills(), Bars.NoGrowth(), new[] { "bandolier", "goodArm", "demolition" }, new[] { ItemIds.FragGrenade, ItemIds.FragGrenade });
            // Two accessories of two grenades, one more each with Buen brazo, and one more with Bandolera.
            Assert.Equal(4 + 2 + 1, carrying.Charges[ChargeIds.Grenade]);
            Assert.Equal(3, carrying.Mods.GrenadeRange);
            Assert.Equal(1, carrying.Mods.GrenadeRadius);
            Assert.Equal(1, carrying.Mods.GrenadeDamage);
            Assert.Equal(0, Bars.FighterStatsOf(Bars.NoSkills(), Bars.NoGrowth(), new[] { "bladeMaster" }, new string[0], new Arms { Primary = null, Melee = null }).Mods.MeleeDamage);
            var machete = Bars.FighterStatsOf(Bars.NoSkills(), Bars.NoGrowth(), new[] { "bladeMaster" }, new string[0], new Arms { Primary = null, Melee = FoundWeaponIds.Machete }).Mods;
            Assert.Equal(2, machete.MeleeDamage);
            Assert.Equal(15, machete.MeleeAim);
        }

        [Fact(DisplayName = "the hex board (docs/FASE10.md §6) › the role: the line with most cells bought, a tie to the one bought last")]
        public void TheRole()
        {
            Assert.Null(Board.BoardRole(new string[0]));
            Assert.Equal(SkillIds.Shooting, Board.BoardRole(new[] { "steadyHand", "grip", "lightTrigger" }));
            Assert.Equal(SkillIds.Melee, Board.BoardRole(new[] { "steadyHand", "grip" }));
        }

        [Fact(DisplayName = "the hex board (docs/FASE10.md §6) › a random board spends every point, each cell beside the ones before")]
        public void ARandomBoardSpendsEveryPoint()
        {
            var skills = BarsFilled((SkillIds.Shooting, 3), (SkillIds.Fitness, 2));
            var board = Bars.RandomBoard(skills, new Rng(7));
            Assert.Equal(5, board.Count);
            for (var i = 0; i < board.Count; i++) Assert.Null(Bars.UnlockBlocker(skills, board.Take(i).ToList(), board[i]));
        }

        // ------------------------------------------------------------ what a soldier does in a mission is counted by the events

        private static (GameState s, Unit a, Unit b, Unit z, Unit z2) CountingField()
        {
            var s = StateFrom(Enumerable.Range(0, 12).Select((_) => new string('.', 12)).ToArray());
            var a = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (u) => u.Owner = 0);
            var b = AddUnit(s, "s2", TemplateIds.Rookie, 2, 1, extra: (u) => u.Owner = 1);
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 6, 1);
            var z2 = AddUnit(s, "z2", TemplateIds.Zombie, 7, 1);
            return (s, a, b, z, z2);
        }

        private static ShotEvent Shot(string unit, string target, string ability, string result)
        {
            return new ShotEvent { Unit = unit, Target = target, Ability = ability, From = P(1, 1, 0), Result = result, Chance = 50, Roll = 10, Reaction = false };
        }

        [Fact(DisplayName = "what a soldier does in a mission is counted by the events › every shot and swing, more if it lands, more if it kills")]
        public void EveryShotAndSwing()
        {
            var (s, a, _, _, _) = CountingField();
            foreach (var e in new GameEvent[]
            {
                Shot("s1", "z1", AbilityIds.Shoot, ShotResults.Hit),
                Shot("s1", "z1", AbilityIds.Shoot, ShotResults.Miss),
                Shot("s1", "z1", AbilityIds.Shoot, ShotResults.Graze),
                new DamagedEvent { Unit = "z1", Amount = 9, Mitigated = 0, Shred = 0 },
                new DiedEvent { Unit = "z1", By = "s1" },
                Shot("s1", "z2", AbilityIds.Slash, ShotResults.Miss),
                Shot("s1", "z2", AbilityIds.Slash, ShotResults.Crit),
                new DiedEvent { Unit = "z2", By = "s1" },
                Shot("s1", "s2", AbilityIds.Shoot, ShotResults.Hit),
            }) Events.ApplyEvent(s, e);
            SameMap(DeedsOf((DeedIds.Shot, 3), (DeedIds.Hit, 2), (DeedIds.GunKill, 1), (DeedIds.Swing, 2), (DeedIds.Blow, 1), (DeedIds.BladeKill, 1)), a.Deeds);
        }

        [Fact(DisplayName = "what a soldier does in a mission is counted by the events › a throw teaches, and once more if it reaches an enemy; a kill with it is no gun kill")]
        public void AThrowTeaches()
        {
            var (s, a, _, _, _) = CountingField();
            foreach (var e in new GameEvent[]
            {
                new ExplosiveEvent { Unit = "s1", Ability = AbilityIds.Grenade, Target = P(6, 1, 0) },
                new DamagedEvent { Unit = "z1", Amount = 3, Mitigated = 0, Shred = 0 },
                new DamagedEvent { Unit = "z2", Amount = 3, Mitigated = 0, Shred = 0 },
                new DiedEvent { Unit = "z2", By = "s1" },
                new ExplosiveEvent { Unit = "s1", Ability = AbilityIds.Flashbang, Target = P(6, 1, 0) },
                new DisorientedEvent { Unit = "z1" },
                new ExplosiveEvent { Unit = "s1", Ability = AbilityIds.Smoke, Target = P(1, 1, 0) },
            }) Events.ApplyEvent(s, e);
            SameMap(DeedsOf((DeedIds.Throw, 3), (DeedIds.ThrowHit, 2)), a.Deeds);
        }

        [Fact(DisplayName = "what a soldier does in a mission is counted by the events › cures, searches, a fixed radio, a silenced alarm, hunkering and running")]
        public void CuresSearchesARadioAnAlarmHunkeringAndRunning()
        {
            var (s, a, b, _, _) = CountingField();
            var relay = AddUnit(s, "r1", TemplateIds.Relay, 9, 9, extra: (u) => u.Objective = true);
            s.Mission.Kind = MissionKinds.Sabotage;
            s.Mission.ObjectiveUnit = relay.Id;
            foreach (var e in new GameEvent[]
            {
                new HealedEvent { Unit = "s1", Target = "s2", Amount = 3 },
                new HealedEvent { Unit = "s1", Target = "s1", Amount = 2 },
                new CuredEvent { Unit = "s2", By = "s1" },
                new SearchedEvent { Unit = "s2", Tile = P(2, 2, 0), Loot = null },
                new RoomSearchedEvent { Unit = "s2", Room = 1, Loot = null },
                new HackedEvent { Unit = "s2", Success = true, Chance = 60 },
                Shot("s1", "r1", AbilityIds.Shoot, ShotResults.Hit),
                new DiedEvent { Unit = "r1", By = "s1" },
                new HunkerEvent { Unit = "s2" },
                new MovedEvent { Unit = "s2", Path = new List<Pos> { P(3, 1, 0) }, Ap = 1 },
                new MovedEvent { Unit = "s2", Path = new List<Pos> { P(9, 1, 0) }, Ap = 2 },
            }) Events.ApplyEvent(s, e);
            SameMap(DeedsOf((DeedIds.HealOther, 2), (DeedIds.HealSelf, 1), (DeedIds.Shot, 1), (DeedIds.Hit, 1), (DeedIds.Fix, 1)), a.Deeds);
            SameMap(DeedsOf((DeedIds.Search, 2), (DeedIds.Fix, 1), (DeedIds.Hunker, 1), (DeedIds.Run, 1)), b.Deeds);
        }

        [Fact(DisplayName = "what a soldier does in a mission is counted by the events › only the squad learns, and a client counts the same as the host")]
        public void OnlyTheSquadLearns()
        {
            var (s, _, _, z, _) = CountingField();
            var client = Events.CloneState(s);
            var r = Exec(s, Ability("s1", AbilityIds.Grenade, tile: P(6, 1)), 0, 3, new[] { 0, 1 });
            Assert.True(r.Ok, r.Error);
            foreach (var e in r.Events!) Events.ApplyEvent(client, e);
            Assert.Null(Step2Json.StateDiff(s, client));
            SameMap(DeedsOf((DeedIds.Throw, 1), (DeedIds.ThrowHit, 1)), s.Units.First((u) => u.Id == "s1").Deeds);
            Assert.Null(z.Deeds);
        }

        // ------------------------------------------------------------ a bar fills out there (Arturo, 16/10)

        [Fact(DisplayName = "a bar fills out there (Arturo, 16/10) › after the action that fills it, its stat goes up at once, the same on every client")]
        public void AfterTheActionThatFillsIt()
        {
            var s = StateFrom(Enumerable.Range(0, 12).Select((_) => new string('.', 12)).ToArray());
            var eight = Bars.NoSkills();
            eight[SkillIds.Shooting] = 8;
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (x) =>
            {
                x.Owner = 0;
                x.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Melee = null };
                x.Lines = Bars.MissionLinesOf(eight, Bars.NoGrowth());
            });
            AddUnit(s, "s2", TemplateIds.Rookie, 1, 3, extra: (x) => x.Owner = 0);
            AddUnit(s, "z1", TemplateIds.Zombie, 6, 1, extra: (x) =>
            {
                x.Hp = 99;
                x.MaxHp = 99;
            });
            u.Mods.Aim = 300;
            var aim = u.Mods.GunAim;
            var client = Events.CloneState(s);
            var r = Exec(s, Ability("s1", AbilityIds.Shoot, "z1"));
            Assert.True(r.Ok, r.Error);
            // A shot and a hit: 8 + 2 = 10, the first bar. It comes after the whole shot.
            var filled = r.Events!.FindIndex((e) => e.T == "barFilled");
            Assert.True(filled > r.Events.FindIndex((e) => e.T == "damaged"), $"barFilled at {filled}");
            var bar = (BarFilledEvent)r.Events[filled];
            Assert.Equal(("s1", SkillIds.Shooting, 1), (bar.Unit, bar.Skill, bar.Filled));
            Assert.True(bar.Growth >= 5, $"{bar.Growth} >= 5");
            Assert.Equal((int)Js.Round(60 * (1 + bar.Growth / 100.0)) - 60, u.Mods.GunAim - aim);
            Assert.Equal(new[] { SkillIds.Shooting }, u.Lines!.Rolls.Keys);
            Assert.Equal(new[] { bar.Growth }, u.Lines.Rolls[SkillIds.Shooting]);
            foreach (var e in r.Events) Events.ApplyEvent(client, e);
            Assert.Null(Step2Json.StateDiff(s, client));
            // Home again: the bar keeps what it rolled out there, and isn't rolled twice.
            var home = Bars.Learn(eight, Bars.NoGrowth(), u.Deeds, new Rng(99), 0, u.Lines);
            Assert.Single(home.Bars);
            Assert.Equal((SkillIds.Shooting, 1), (home.Bars[0].Skill, home.Bars[0].Filled));
            Assert.Equal(new[] { bar.Growth }, home.Bars[0].Growth);
            Assert.Equal(bar.Growth, home.Growth[SkillIds.Shooting]);
        }

        [Fact(DisplayName = "a bar fills out there (Arturo, 16/10) › Forma física: health grows at once, the new points full")]
        public void FormaFisicaHealthGrowsAtOnce()
        {
            var s = StateFrom(new[] { "...." });
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 0, 0, extra: (x) =>
            {
                x.Owner = 0;
                x.Hp = 3;
                x.MaxHp = 5;
                x.Lines = Bars.MissionLinesOf(Bars.NoSkills(), GrowthOf((SkillIds.Fitness, 15)));
            });
            // 5 grown 15 % is 6 (5.75); 25 % more is 6 (6.25); 35 %, 7 (6.75).
            Events.ApplyEvent(s, new BarFilledEvent { Unit = "s1", Skill = SkillIds.Fitness, Filled = 1, Growth = 10 });
            Assert.Equal((5, 3), (u.MaxHp, u.Hp));
            Events.ApplyEvent(s, new BarFilledEvent { Unit = "s1", Skill = SkillIds.Fitness, Filled = 2, Growth = 10 });
            Assert.Equal((6, 4), (u.MaxHp, u.Hp));
        }

        // ------------------------------------------------------------ the new actions (docs/FASE10.md §4)

        private static GameState Field16()
        {
            return StateFrom(Enumerable.Range(0, 16).Select((_) => new string('.', 16)).ToArray());
        }

        /// <summary>`{ owner: 0, ...armed }`: an assault rifle in the hand, the knife at the belt.</summary>
        private static void Armed(Unit u)
        {
            u.Owner = 0;
            u.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Melee = null };
        }

        private static Unit Hardy(GameState s, string id, int x, int y, int hp)
        {
            return AddUnit(s, id, TemplateIds.Zombie, x, y, extra: (u) =>
            {
                u.Hp = hp;
                u.MaxHp = hp;
            });
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › a stone is always at hand, and the blade can keep watch")]
        public void AStoneIsAlwaysAtHand()
        {
            var s = Field16();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: Armed);
            Assert.Contains(AbilityIds.Stone, Abilities.AbilitiesOf(u));
            Assert.Contains(AbilityIds.OverwatchMelee, Abilities.AbilitiesOf(u));
            var list = Abilities.AbilitiesOf(u);
            Assert.Equal(list.IndexOf(AbilityIds.Overwatch) + 1, list.IndexOf(AbilityIds.OverwatchMelee));
            Assert.DoesNotContain(AbilityIds.Stone, Abilities.AbilitiesOf(AddUnit(s, "z1", TemplateIds.Zombie, 1, 1)));
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › the stone: heard where it lands, 1 of damage to whoever is there, and the turn goes on")]
        public void TheStone()
        {
            var s = Field16();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 2, 2, extra: Armed);
            var z = Hardy(s, "z1", 8, 2, 5);
            var far = AddUnit(s, "z2", TemplateIds.Zombie, 14, 14);
            var r = Exec(s, Ability("s1", AbilityIds.Stone, tile: P(8, 2)));
            Assert.True(r.Ok, r.Error);
            var stone = r.Events!.OfType<StoneThrownEvent>().First();
            Assert.Equal(("s1", "z1"), (stone.Unit, stone.Hit));
            Assert.Equal(4, z.Hp);
            Assert.Equal(1, u.Ap);
            var noise = r.Events!.OfType<NoiseEvent>().First();
            Assert.Equal((8, 2), (noise.Tile.X, noise.Tile.Y));
            Assert.Equal(Js.Round(8 * Rules.SquadLouder * 10) / 10, noise.Radius);
            Assert.True(far.Alive);
            SameMap(DeedsOf((DeedIds.Throw, 1), (DeedIds.ThrowHit, 1)), u.Deeds);
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › a stone on the floor only sounds; Señuelo makes it heard further, Canto afilado hurts more")]
        public void AStoneOnTheFloorOnlySounds()
        {
            var s = Field16();
            var st = Bars.FighterStatsOf(Bars.NoSkills(), Bars.NoGrowth(), new[] { "decoy", "sharpStone" });
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 2, 2, extra: (x) =>
            {
                Armed(x);
                x.Mods = st.Mods;
            });
            AddUnit(s, "z1", TemplateIds.Zombie, 14, 14);
            var r = Exec(s, Ability("s1", AbilityIds.Stone, tile: P(6, 6)));
            Assert.True(r.Ok, r.Error);
            Assert.Null(r.Events!.OfType<StoneThrownEvent>().First().Hit);
            Assert.Equal(Js.Round(12 * Rules.SquadLouder * 10) / 10, r.Events!.OfType<NoiseEvent>().First().Radius);
            SameMap(DeedsOf((DeedIds.Throw, 1)), u.Deeds);
            var t = Field16();
            AddUnit(t, "s1", TemplateIds.Rookie, 2, 2, extra: (x) =>
            {
                Armed(x);
                x.Mods = st.Mods;
            });
            var z = Hardy(t, "z1", 4, 2, 5);
            Assert.True(Exec(t, Ability("s1", AbilityIds.Stone, tile: P(4, 2))).Ok);
            Assert.Equal(3, z.Hp);
        }

        private static List<Pos> Walk(int x, int from, int to)
        {
            return Enumerable.Range(0, from - to).Select((i) => P(x, from - 1 - i, 0)).ToList();
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › melee overwatch strikes the first enemy that comes alongside, not the one walking past in sight, and stops")]
        public void MeleeOverwatchStrikesTheFirstEnemyAlongside()
        {
            var s = Field16();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (x) =>
            {
                Armed(x);
                x.Ap = 2;
            });
            // Someone with actions left, so the turn doesn't pass.
            AddUnit(s, "s2", TemplateIds.Rookie, 1, 1, extra: Armed);
            AddUnit(s, "z9", TemplateIds.Zombie, 15, 15);
            var r = Exec(s, Ability("s1", AbilityIds.OverwatchMelee));
            Assert.True(r.Ok, r.Error);
            Assert.Equal((true, (bool?)true, 0), (u.Overwatch, u.WatchMelee, u.Ap));
            var passer = Hardy(s, "z0", 9, 12, 50);
            var z = Hardy(s, "z1", 5, 9, 50);
            var z2 = Hardy(s, "z2", 4, 9, 50);
            var sim = NewSim(s);
            sim.MoveAlong(passer, Walk(9, 12, 8), 1);
            Assert.DoesNotContain(sim.Events, (e) => e.T == "shot");
            sim.MoveAlong(z, Walk(5, 9, 6), 1);
            var strikes = Of<ShotEvent>(sim.Events);
            Assert.Single(strikes);
            Assert.Equal(("s1", "z1", AbilityIds.Slash, true), (strikes[0].Unit, strikes[0].Target, strikes[0].Ability, strikes[0].Reaction));
            Assert.False(u.Overwatch);
            sim.MoveAlong(z2, Walk(4, 9, 6), 1);
            Assert.Single(Of<ShotEvent>(sim.Events));
            Assert.Equal(1, u.Deeds?.GetValueOrDefault(DeedIds.Swing));
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › Muro de acero: every enemy that comes alongside, once each")]
        public void MuroDeAcero()
        {
            var s = Field16();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (x) =>
            {
                Armed(x);
                x.Ap = 2;
                x.Perks = new List<string> { PerkIds.BladeWall };
            });
            AddUnit(s, "s2", TemplateIds.Rookie, 1, 1, extra: Armed);
            AddUnit(s, "z9", TemplateIds.Zombie, 15, 15);
            Assert.True(Exec(s, Ability("s1", AbilityIds.OverwatchMelee)).Ok);
            var z = Hardy(s, "z1", 5, 9, 50);
            var z2 = Hardy(s, "z2", 4, 9, 50);
            var sim = NewSim(s);
            sim.MoveAlong(z, Walk(5, 9, 6), 1);
            sim.MoveAlong(z2, Walk(4, 9, 6), 1);
            Assert.Equal(new[] { "z1", "z2" }, Of<ShotEvent>(sim.Events).Select((e) => e.Target));
            Assert.True(u.Overwatch);
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › Segundo aliento: a blade kill heals 1")]
        public void SegundoAliento()
        {
            var s = Field16();
            var mods = Bars.FighterStatsOf(Bars.NoSkills(), Bars.NoGrowth(), new string[0]).Mods;
            mods.MeleeAim = 200;
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (x) =>
            {
                Armed(x);
                x.Perks = new List<string> { PerkIds.SecondWind };
                x.Hp = 2;
                x.MaxHp = 6;
                x.Mods = mods;
            });
            Hardy(s, "z1", 6, 5, 1);
            AddUnit(s, "z9", TemplateIds.Zombie, 15, 15);
            var r = Exec(s, Ability("s1", AbilityIds.Slash, "z1"));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(3, u.Hp);
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › Hacerse sitio: a Slash at every enemy alongside, none at the one further off")]
        public void HacerseSitio()
        {
            var s = Field16();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (x) =>
            {
                x.Owner = 0;
                x.Perks = new List<string> { PerkIds.ElbowRoom };
                x.Arms = new Arms { Primary = null, Melee = FoundWeaponIds.Machete };
            });
            Hardy(s, "z1", 4, 5, 99);
            Hardy(s, "z2", 6, 6, 99);
            Hardy(s, "z3", 9, 9, 99);
            var r = Exec(s, Ability("s1", AbilityIds.ElbowRoom));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(new[] { "z1", "z2" }, Of<ShotEvent>(r.Events!).Select((e) => e.Target).OrderBy((t) => t, StringComparer.Ordinal));
            Assert.True(u.Cooldowns.TryGetValue(AbilityIds.ElbowRoom, out var cooldown) && cooldown > 0, $"cooldown {cooldown}");
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › Espabilar needs no medkit, only a partner alongside")]
        public void EspabilarNeedsNoMedkit()
        {
            var s = Field16();
            AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (x) =>
            {
                x.Owner = 0;
                x.Perks = new List<string> { PerkIds.RevivalProtocol };
            });
            var dazed = AddUnit(s, "s2", TemplateIds.Rookie, 6, 5, extra: (x) =>
            {
                x.Owner = 0;
                x.Stunned = true;
                x.Disoriented = 1;
            });
            var far = AddUnit(s, "s3", TemplateIds.Rookie, 9, 5, extra: (x) =>
            {
                x.Owner = 0;
                x.Panicked = true;
            });
            AddUnit(s, "z1", TemplateIds.Zombie, 11, 11);
            Assert.False(Exec(s, Ability("s1", AbilityIds.Revival, "s3")).Ok);
            Assert.True(Exec(s, Ability("s1", AbilityIds.Revival, "s2")).Ok);
            Assert.Equal((false, 0), (dazed.Stunned, dazed.Disoriented));
            Assert.True(far.Panicked);
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › first aid heals more with what Curar has grown and with Vendaje")]
        public void FirstAidHealsMore()
        {
            var s = Field16();
            var st = Bars.FighterStatsOf(Bars.NoSkills(), GrowthOf((SkillIds.Medicine, 26)), new[] { "bandage", "firstAid", "bigKit" }, new[] { ItemIds.FirstAid });
            // Two uses of the Botiquín, and one more with Botiquín grande.
            Assert.Equal(3, st.Charges[ChargeIds.FirstAid]);
            AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (x) =>
            {
                x.Owner = 0;
                x.Gear = new List<string> { ItemIds.FirstAid };
                x.Charges = st.Charges;
                x.Mods = st.Mods;
            });
            var hurt = AddUnit(s, "s2", TemplateIds.Rookie, 6, 5, extra: (x) =>
            {
                x.Owner = 0;
                x.Hp = 1;
                x.MaxHp = 12;
            });
            AddUnit(s, "z1", TemplateIds.Zombie, 15, 15);
            var r = Exec(s, Ability("s1", AbilityIds.FirstAid, "s2"));
            Assert.True(r.Ok, r.Error);
            // 4 grown 26 % is 5 (5.04), and +1 for Vendaje; and Primeros auxilios shelters whoever it heals.
            Assert.Equal(1 + 5 + 1, hurt.Hp);
            Assert.Equal(10, hurt.Aided);
        }

        [Fact(DisplayName = "the new actions (docs/FASE10.md §4) › Mantenimiento: the weapon wears half")]
        public void MantenimientoTheWeaponWearsHalf()
        {
            var s = Field16();
            var plain = AddUnit(s, "s1", TemplateIds.Rookie, 2, 2, extra: (x) =>
            {
                x.Owner = 0;
                x.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Melee = null, Condition = 100 };
            });
            var care = Content.AddMods(plain.Mods, new UnitMods { Care = 1 }); // `{ ...plain.mods, care: 1 }`: plain's care is 0.
            var careful = AddUnit(s, "s2", TemplateIds.Rookie, 2, 4, extra: (x) =>
            {
                x.Owner = 0;
                x.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, Melee = null, Condition = 100 };
                x.Mods = care;
            });
            foreach (var u in new[] { plain, careful }) Events.ApplyEvent(s, new ShotEvent { Unit = u.Id, Target = "zz", Ability = AbilityIds.Shoot, From = u.Pos, Result = ShotResults.Miss, Chance = 50, Roll = 90, Reaction = false });
            Assert.Equal((100 - plain.Arms!.Condition!.Value) / 2, 100 - careful.Arms!.Condition!.Value);
        }
    }
}
