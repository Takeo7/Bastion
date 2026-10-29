#nullable enable
// skills.ts: Fase 10, experience bars (docs/FASE10.md, Arturo 16/10). Six lines that
// fill with what each soldier does in a mission (Unit.deeds, counted by the
// events) and is added if it comes back alive. A bar has no levels: when it
// fills it starts again, every time longer, and gives a skill point (spent on
// the hex board, board.ts) and 5–10 % more, at random, of its line's stat.
// Veterancy goes up with the points spent on the board (Arturo, 16/10).
// The class is Bars, not Skills: that is the type of a soldier's experience (SkillTypes.cs).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public sealed class SkillDef
    {
        public string Id { get; set; } = ""; // SkillIds
        public string Name { get; set; } = "";
        public string Short { get; set; } = "";
        /// <summary>What fills it.</summary>
        public string Line { get; set; } = "";
        /// <summary>The stat it raises.</summary>
        public string Stat { get; set; } = "";
    }

    /// <summary>barsOf's `{ filled: number; into: number; need: number }`.</summary>
    public sealed class BarProgress
    {
        public int Filled { get; set; }
        public int Into { get; set; }
        public int Need { get; set; }
    }

    public sealed class VeterancyDef
    {
        public string Name { get; set; } = "";
        /// <summary>For a woman: Novata, Fogueada… (Lore).</summary>
        public string Female { get; set; } = "";
        /// <summary>Skill points spent on the board it starts at (a step every RANK_STEP).</summary>
        public int From { get; set; }
        public string Line { get; set; } = "";
    }

    /// <summary>growthDelta's `{ mods: Partial&lt;UnitMods&gt;; hp: number }`: the fields `mods` doesn't name are 0.</summary>
    public sealed class GrowthDelta
    {
        public UnitMods Mods { get; set; } = new UnitMods();
        public int Hp { get; set; }
    }

    /// <summary>Everything a soldier fights with, from its lines, its talents and what it carries.</summary>
    public sealed class FighterStats
    {
        public string Template { get; set; } = ""; // TemplateIds
        public UnitMods Mods { get; set; } = new UnitMods();
        public int MaxHp { get; set; }
        public Dictionary<string, int> Charges { get; set; } = new Dictionary<string, int>(); // ChargeIds → uses (Partial<Record>)
        public int Will { get; set; }
        public int Armor { get; set; }
        public List<string> Perks { get; set; } = new List<string>(); // PerkIds
        /// <summary>VETERANCY's index (Unit.rank).</summary>
        public int Rank { get; set; }
    }

    /// <summary>A bar that filled on coming back: its line, how many it has now, and what each one added (percent).</summary>
    public sealed class BarFilled
    {
        public string Skill { get; set; } = ""; // SkillIds
        public int Filled { get; set; }
        public List<int> Growth { get; set; } = new List<int>();
    }

    /// <summary>pendingBars's `{ skill: SkillId; filled: number }`.</summary>
    public sealed class PendingBar
    {
        public string Skill { get; set; } = ""; // SkillIds
        public int Filled { get; set; }
    }

    /// <summary>learn's `{ skills; growth; gained; bars; points }`.</summary>
    public sealed class Learned
    {
        public Skills Skills { get; set; } = new Skills();
        public Growth Growth { get; set; } = new Growth();
        public Skills Gained { get; set; } = new Skills();
        public List<BarFilled> Bars { get; set; } = new List<BarFilled>();
        public int Points { get; set; }
    }

    /// <summary>`{ skills: Skills; growth: Growth }` (recruitSkills, oneMoreBar), and the board of a skirmish veteran (veteranSkills).</summary>
    public sealed class SkillLines
    {
        public Skills Skills { get; set; } = new Skills();
        public Growth Growth { get; set; } = new Growth();
        /// <summary>veteranSkills's only.</summary>
        public List<string>? Board { get; set; }
    }

    public static class Bars
    {
        // ------------------------------------------------------------------- lines

        /// <summary>NO_SKILLS: every line at 0, a new one each time (`{ ...NO_SKILLS }`).</summary>
        public static Skills NoSkills()
        {
            var out_ = new Skills();
            foreach (var id in SkillIds.All) out_[id] = 0;
            return out_;
        }

        /// <summary>NO_GROWTH: every line at 0, a new one each time.</summary>
        public static Growth NoGrowth()
        {
            var out_ = new Growth();
            foreach (var id in SkillIds.All) out_[id] = 0;
            return out_;
        }

        private static Skills Copy(Skills s)
        {
            var out_ = new Skills();
            foreach (var e in s) out_[e.Key] = e.Value;
            return out_;
        }

        private static Growth Copy(Growth g)
        {
            var out_ = new Growth();
            foreach (var e in g) out_[e.Key] = e.Value;
            return out_;
        }

        private static int Of(IReadOnlyDictionary<string, int> d, string id)
        {
            return d.TryGetValue(id, out var n) ? n : 0;
        }

        public static readonly IReadOnlyDictionary<string, SkillDef> SkillDefs = new Dictionary<string, SkillDef>
        {
            [SkillIds.Shooting] = new SkillDef { Id = SkillIds.Shooting, Name = "Disparo", Short = "DIS", Line = "Se llena disparando, más si se acierta y más si se abate.", Stat = "Puntería con armas de fuego" },
            [SkillIds.Melee] = new SkillDef { Id = SkillIds.Melee, Name = "Cuerpo a cuerpo", Short = "CaC", Line = "Se llena golpeando con la hoja, más si se acierta y más si se abate.", Stat = "Puntería cuerpo a cuerpo" },
            [SkillIds.Throwing] = new SkillDef { Id = SkillIds.Throwing, Name = "Lanzar", Short = "LAN", Line = "Se llena lanzando piedras y granadas, más si alcanzan a alguien.", Stat = "Alcance al lanzar" },
            [SkillIds.Medicine] = new SkillDef { Id = SkillIds.Medicine, Name = "Curar", Short = "CUR", Line = "Se llena curando con el botiquín o con medicinas; más si es a otro.", Stat = "Curación" },
            [SkillIds.Searching] = new SkillDef { Id = SkillIds.Searching, Name = "Investigar", Short = "INV", Line = "Se llena registrando habitaciones, abriendo cajas, arreglando equipos y apagando alarmas.", Stat = "Destreza al arreglar" },
            [SkillIds.Fitness] = new SkillDef { Id = SkillIds.Fitness, Name = "Forma física", Short = "FOR", Line = "Se llena cubriéndose y corriendo (moverse con las dos acciones).", Stat = "Salud" },
        };

        /// <summary>The first bar takes BAR_FIRST, and every next one BAR_STEP more.</summary>
        public const int BarFirst = 10;
        public const int BarStep = 5;
        /// <summary>What a bar adds to its line's stat when it fills, in percent, at random between these.</summary>
        public static readonly IReadOnlyList<int> BarGrowth = new[] { 5, 10 };

        /// <summary>Experience the `n`-th bar of a line takes (0 is the first).</summary>
        public static int BarCost(int n)
        {
            return BarFirst + BarStep * n;
        }

        /// <summary>Experience it takes to fill the first `n` bars of a line.</summary>
        public static int BarsCost(int n)
        {
            return BarFirst * n + (BarStep * n * (n - 1)) / 2;
        }

        /// <summary>A line's bars filled, and how far into the next one it is.</summary>
        public static BarProgress BarsOf(int xp)
        {
            var filled = 0;
            while (xp >= BarsCost(filled + 1)) filled++;
            return new BarProgress { Filled = filled, Into = xp - BarsCost(filled), Need = BarCost(filled) };
        }

        /// <summary>What a bar adds when it fills: 5 to 10 % (BAR_GROWTH), at random.</summary>
        public static int RollGrowth(Rng rng)
        {
            return rng.Range(BarGrowth[0], BarGrowth[1]);
        }

        // --------------------------------------------------------------- veterancy

        public static readonly IReadOnlyList<VeterancyDef> Veterancy = new[]
        {
            new VeterancyDef { Name = "Novato", Female = "Novata", From = 0, Line = "Sin rango: menos de 5 puntos gastados en el tablero." },
            new VeterancyDef { Name = "Fogueado", Female = "Fogueada", From = 5, Line = "+10 de voluntad." },
            new VeterancyDef { Name = "Curtido", Female = "Curtida", From = 10, Line = "+20 de voluntad." },
            new VeterancyDef { Name = "Veterano", Female = "Veterana", From = 15, Line = "+30 de voluntad y una tercera acción por turno." },
            new VeterancyDef { Name = "Superviviente", Female = "Superviviente", From = 20, Line = "+40 de voluntad y una tercera acción por turno." },
        };

        /// <summary>A rank more for every so many points spent on the board, and this much will with each (Arturo, 16/10).</summary>
        public const int RankStep = 5;
        public const int WillPerRank = 10;

        public const int BaseHp = 5;
        public const int BaseWill = 40;
        /// <summary>Aim with a firearm or a blade before what the lines add, and before the weapon.</summary>
        public const int BaseAim = 60;
        /// <summary>How far a throw reaches before what Lanzar adds (the grenade's range).</summary>
        public const int ThrowRange = 10;

        /// <summary>Bars filled among the six lines: the skill points earned.</summary>
        public static int TotalBars(Skills skills)
        {
            return SkillIds.All.Sum((id) => BarsOf(Of(skills, id)).Filled);
        }

        /// <summary>VETERANCY's index for the points spent on the board; from Veterano on, a third action (content.ts THIRD_ACTION_RANK).</summary>
        public static int VeterancyRank(int spent)
        {
            var rank = 0;
            while (rank + 1 < Veterancy.Count && spent >= Veterancy[rank + 1].From) rank++;
            return rank;
        }

        /// <summary>Will for a rank: 40, and 10 more with each.</summary>
        public static int VeteranWill(int rank)
        {
            return BaseWill + WillPerRank * rank;
        }

        // ------------------------------------------------------------------- stats

        /// <summary>`base` with `percent` more, rounded: a stat after what its line has added.</summary>
        public static int Grown(int @base, int percent)
        {
            return (int)Js.Round(@base * (1 + percent / 100.0));
        }

        /// <summary>LINE_STAT's base of each line: what its stat is before what its bars add.</summary>
        private static int LineBase(string skill)
        {
            switch (skill)
            {
                case SkillIds.Shooting:
                case SkillIds.Melee:
                    return BaseAim;
                case SkillIds.Throwing:
                    return ThrowRange;
                case SkillIds.Medicine:
                    return Content.MedkitHeal;
                case SkillIds.Searching:
                    return Content.HackBase;
                default:
                    return BaseHp;
            }
        }

        /// <summary>
        /// What a line's growth going from `from` to `to` percent adds: to its mod, or to health. The mod each line grows
        /// (LINE_STAT): gunAim, meleeAim, grenadeRange, heal, hack; none for Forma física, health.
        /// </summary>
        public static GrowthDelta GrowthDeltaOf(string skill, int from, int to)
        {
            var @base = LineBase(skill);
            var more = Grown(@base, to) - Grown(@base, from);
            var mods = new UnitMods();
            switch (skill)
            {
                case SkillIds.Shooting:
                    mods.GunAim = more;
                    break;
                case SkillIds.Melee:
                    mods.MeleeAim = more;
                    break;
                case SkillIds.Throwing:
                    mods.GrenadeRange = more;
                    break;
                case SkillIds.Medicine:
                    mods.Heal = more;
                    break;
                case SkillIds.Searching:
                    mods.Hack = more;
                    break;
                default:
                    return new GrowthDelta { Mods = mods, Hp = more };
            }
            return new GrowthDelta { Mods = mods, Hp = 0 };
        }

        /// <summary>
        /// A soldier's numbers (docs/FASE10.md): each line's stat with what its bars
        /// added (`growth`), the skills unlocked on the board, and what it carries.
        /// Everyone fights as the same template; grenades, the medkit and the rest
        /// come only from the belt.
        /// </summary>
        public static FighterStats FighterStatsOf(Skills skills, Growth growth, IReadOnlyList<string> board, IReadOnlyList<string>? gear = null, Arms? arms = null)
        {
            gear ??= new string[0];
            var tpl = Content.Templates[TemplateIds.Rookie];
            var mods = Content.AddMods(Content.ZeroMods, new UnitMods { Aim = BaseAim - tpl.Aim });
            var maxHp = BaseHp;
            foreach (var id in SkillIds.All)
            {
                var delta = GrowthDeltaOf(id, 0, Of(growth, id));
                mods = Content.AddMods(mods, delta.Mods);
                maxHp += delta.Hp;
            }
            var armor = 0;
            var perks = new List<string>();
            var charges = new Dictionary<string, int>();
            void Add(IReadOnlyDictionary<string, int>? extra)
            {
                if (extra == null) return;
                foreach (var e in extra) if (e.Value != 0) charges[e.Key] = (charges.TryGetValue(e.Key, out var n) ? n : 0) + e.Value;
            }
            var cells = board.Select((id) => Board.BoardCells.TryGetValue(id, out var c) ? c : null).Where((c) => c != null).Select((c) => c!).ToList();
            foreach (var c in cells)
            {
                if (c.Mods != null) mods = Content.AddMods(mods, c.Mods);
                maxHp += c.Hp ?? 0;
                armor += c.Armor ?? 0;
                if (c.Perk != null && !perks.Contains(c.Perk)) perks.Add(c.Perk);
                Add(c.Charges);
            }
            foreach (var item in gear.Select((id) => Content.Items[id]))
            {
                if (item.Mods != null) mods = Content.AddMods(mods, item.Mods);
                maxHp += item.Hp ?? 0;
                Add(item.Charges);
            }
            // Mano de machete wants the machete; Buen brazo, one more grenade in every accessory of them.
            if (perks.Contains(PerkIds.BladeMaster) && arms?.Melee == FoundWeaponIds.Machete) mods = Content.AddMods(mods, new UnitMods { MeleeDamage = 2, MeleeAim = 15 });
            // The chainsaw, with its fuel for the mission.
            if (arms?.Melee == FoundWeaponIds.Chainsaw) Add(new Dictionary<string, int> { [ChargeIds.Fuel] = Content.ChainsawFuel });
            if (perks.Contains(PerkIds.GoodArm)) Add(new Dictionary<string, int> { [ChargeIds.Grenade] = gear.Count((id) => Content.Items[id].Charges != null && Of(Content.Items[id].Charges!, ChargeIds.Grenade) != 0) });
            // One more use of what it carries: nothing if it carries none.
            foreach (var c in cells)
            {
                if (c.Spare == null) continue;
                foreach (var e in c.Spare) if (charges.ContainsKey(e.Key)) charges[e.Key] += e.Value;
            }
            var rank = VeterancyRank(cells.Count);
            return new FighterStats { Template = TemplateIds.Rookie, Mods = mods, MaxHp = maxHp, Charges = charges, Will = VeteranWill(rank) + mods.Will, Armor = armor, Perks = perks, Rank = rank };
        }

        // -------------------------------------------------------------- the board

        /// <summary>Skill points: one for every bar filled, less the cells unlocked on the board.</summary>
        public static int PointsLeft(Skills skills, IReadOnlyList<string> board)
        {
            return Math.Max(0, TotalBars(skills) - board.Count);
        }

        /// <summary>
        /// Why a soldier can't unlock `id` now (docs/FASE10.md §6), or null: beside the
        /// core or a cell it has, and a point to spend. Nothing else (Arturo, 16/10).
        /// </summary>
        public static string? UnlockBlocker(Skills skills, IReadOnlyList<string> board, string id)
        {
            if (!Board.BoardCells.ContainsKey(id)) return "Esa casilla no existe.";
            if (id == Board.Core || board.Contains(id)) return "Ya la tiene.";
            if (!Board.WithinReach(board, id)) return "Tiene que estar al lado de una que ya tenga.";
            if (PointsLeft(skills, board) < 1) return "No le quedan puntos de habilidad: se gana uno cada vez que se llena una barra.";
            return null;
        }

        /// <summary>A cell's state for a soldier: unlocked, beside one it has (to be bought, if it can), or out of reach.</summary>
        /// <returns>`'unlocked' | 'open' | 'locked'`.</returns>
        public static string CellState(IReadOnlyList<string> board, string id)
        {
            if (id == Board.Core || board.Contains(id)) return "unlocked";
            return Board.WithinReach(board, id) ? "open" : "locked";
        }

        // ---------------------------------------------------------------- learning

        /// <summary>The line each deed fills, and how much (docs/FASE10.md §3).</summary>
        public static readonly IReadOnlyDictionary<string, (string Skill, int Xp)> DeedXp = new Dictionary<string, (string, int)>
        {
            [DeedIds.Shot] = (SkillIds.Shooting, 1),
            [DeedIds.Hit] = (SkillIds.Shooting, 1),
            [DeedIds.GunKill] = (SkillIds.Shooting, 1),
            [DeedIds.Swing] = (SkillIds.Melee, 1),
            [DeedIds.Blow] = (SkillIds.Melee, 1),
            [DeedIds.BladeKill] = (SkillIds.Melee, 1),
            [DeedIds.Throw] = (SkillIds.Throwing, 1),
            [DeedIds.ThrowHit] = (SkillIds.Throwing, 1),
            [DeedIds.HealOther] = (SkillIds.Medicine, 2),
            [DeedIds.HealSelf] = (SkillIds.Medicine, 1),
            [DeedIds.Search] = (SkillIds.Searching, 2),
            [DeedIds.Fix] = (SkillIds.Searching, 3),
            [DeedIds.Hunker] = (SkillIds.Fitness, 1),
            [DeedIds.Run] = (SkillIds.Fitness, 1),
        };

        /// <summary>The experience a mission's deeds give, line by line; `bonus` (0.2 = 20 %) more of each, rounded up.</summary>
        public static Skills LearnedFrom(Deeds? deeds, double bonus = 0)
        {
            var out_ = NoSkills();
            if (deeds != null)
            {
                foreach (var e in deeds)
                {
                    var (skill, xp) = DeedXp[e.Key];
                    out_[skill] += xp * e.Value;
                }
            }
            if (bonus != 0) foreach (var id in SkillIds.All) out_[id] += (int)Math.Ceiling(out_[id] * bonus);
            return out_;
        }

        public static MissionLines MissionLinesOf(Skills skills, Growth growth, double bonus = 0)
        {
            return new MissionLines { Start = Copy(skills), Bonus = bonus, Growth = Copy(growth), Rolls = new Dictionary<string, List<int>>() };
        }

        /// <summary>The experience of each line right now: what it set out with and what its deeds have taught.</summary>
        public static Skills LiveSkills(MissionLines lines, Deeds? deeds)
        {
            var gained = LearnedFrom(deeds, lines.Bonus);
            var out_ = Copy(lines.Start);
            foreach (var id in SkillIds.All) out_[id] = Of(out_, id) + gained[id];
            return out_;
        }

        /// <summary>Bars that have filled out there and not been rolled yet: each one's line and its number.</summary>
        public static List<PendingBar> PendingBars(MissionLines lines, Deeds? deeds)
        {
            var now = LiveSkills(lines, deeds);
            var out_ = new List<PendingBar>();
            foreach (var id in SkillIds.All)
            {
                var done = BarsOf(Of(lines.Start, id)).Filled + (lines.Rolls.TryGetValue(id, out var rolls) ? rolls.Count : 0);
                for (var k = done + 1; k <= BarsOf(now[id]).Filled; k++) out_.Add(new PendingBar { Skill = id, Filled = k });
            }
            return out_;
        }

        /// <summary>
        /// A mission's deeds (and its `bonus`) added to a soldier's lines: the bars
        /// that filled, each with its stat rolled up 5–10 % (`rng`), and a skill point
        /// for each. With `lines`, the bars already filled out there keep what they
        /// rolled; only the rest (with more bonus at home, or the last blow) roll now.
        /// </summary>
        public static Learned Learn(Skills skills, Growth growth, Deeds? deeds, Rng rng, double bonus = 0, MissionLines? lines = null)
        {
            var gained = LearnedFrom(deeds, bonus);
            var next = Copy(skills);
            var grew = Copy(lines?.Growth ?? growth);
            var bars = new List<BarFilled>();
            var points = 0;
            foreach (var id in SkillIds.All)
            {
                var from = BarsOf(Of(skills, id)).Filled;
                next[id] = Of(skills, id) + gained[id];
                var to = BarsOf(next[id]).Filled;
                var out_ = lines != null && lines.Rolls.TryGetValue(id, out var r) ? r : new List<int>();
                var now = new List<int>();
                for (var k = 0; k < Math.Max(0, to - from - out_.Count); k++) now.Add(RollGrowth(rng));
                grew[id] = Of(grew, id) + now.Sum();
                var rolls = out_.Concat(now).ToList();
                if (rolls.Count == 0) continue;
                points += rolls.Count;
                bars.Add(new BarFilled { Skill = id, Filled = from + rolls.Count, Growth = rolls });
            }
            return new Learned { Skills = next, Growth = grew, Gained = gained, Bars = bars, Points = points };
        }

        // ----------------------------------------------------------------- newcomers

        /// <summary>Lines with `bars` filled, each with its growth rolled.</summary>
        private static SkillLines Seasoned(Rng rng, IReadOnlyDictionary<string, int> bars)
        {
            var skills = NoSkills();
            var growth = NoGrowth();
            foreach (var id in SkillIds.All)
            {
                skills[id] = BarsCost(bars[id]);
                for (var k = 0; k < bars[id]; k++) growth[id] += RollGrowth(rng);
            }
            return new SkillLines { Skills = skills, Growth = growth };
        }

        /// <summary>A recruit (docs/FASE10.md §5): a bar filled in one or two lines, at random, so no two are alike.</summary>
        public static SkillLines RecruitSkills(Rng rng)
        {
            var bars = NoSkills();
            var first = rng.Pick(SkillIds.All);
            bars[first] = 1;
            if (rng.Next() < 0.5) bars[rng.Pick(SkillIds.All.Where((id) => id != first).ToList())] = 1;
            return Seasoned(rng, bars);
        }

        /// <summary>One more bar in a line at random, with its growth (a recruit trained in the base, a newcomer who has been out).</summary>
        public static SkillLines OneMoreBar(Skills skills, Growth growth, Rng rng)
        {
            var id = rng.Pick(SkillIds.All);
            var filled = BarsOf(Of(skills, id)).Filled + 1;
            var nextSkills = Copy(skills);
            nextSkills[id] = Math.Max(Of(skills, id), BarsCost(filled));
            var nextGrowth = Copy(growth);
            nextGrowth[id] = Of(growth, id) + RollGrowth(rng);
            return new SkillLines { Skills = nextSkills, Growth = nextGrowth };
        }

        /// <summary>A skirmish veteran (docs/FASE10.md §7): two to five bars in every line, and its points spent on the board at random.</summary>
        public static SkillLines VeteranSkills(Rng rng)
        {
            var bars = NoSkills();
            foreach (var id in SkillIds.All) bars[id] = rng.Range(2, 5);
            var lines = Seasoned(rng, bars);
            lines.Board = RandomBoard(lines.Skills, rng);
            return lines;
        }

        /// <summary>Every point spent, a cell at a time, on whatever the board allows (a skirmish veteran's).</summary>
        public static List<string> RandomBoard(Skills skills, Rng rng)
        {
            var board = new List<string>();
            for (;;)
            {
                var open = Board.Cells.Where((c) => UnlockBlocker(skills, board, c.Id) == null).ToList();
                if (open.Count == 0) return board;
                board.Add(rng.Pick(open).Id);
            }
        }
    }
}
