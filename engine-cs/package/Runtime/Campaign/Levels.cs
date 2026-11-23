#nullable enable
// N9 (Arturo, 22/11, cuadro 114; .dev-snapshots/gameplay/notas-22-11/DIFICULTAD.md): the game's level, chosen when it is made and
// never changed. What changes from one to another: how many of the dead there are and the horde; what is found and paid. Not
// health, damage nor what one starts with. Supervivencia, besides: wounds don't heal by themselves. Without soldiers, lost, at
// every level (N8). Not a stage's difficulty (RunMap.DifficultyOf: 1–3 by row and chapter), which is something else.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>The levels' ids (CampaignState.GameLevel).</summary>
    public static class LevelIds
    {
        public const string Easy = "easy";
        public const string Medium = "medium";
        public const string Hard = "hard";
        public const string Survival = "survival";

        public static readonly IReadOnlyList<string> All = new[] { Easy, Medium, Hard, Survival };
    }

    /// <summary>A level's numbers (to size with the bots' R2: DIFICULTAD.md).</summary>
    public sealed class LevelDef
    {
        public string Id { get; set; } = "";
        /// <summary>The dead on a map: its groups and its lone ones, that share of a hard game's.</summary>
        public double Dead { get; set; } = 1;
        /// <summary>The horde: its trickle that many times, and a wave that much sooner (MissionState.HordeScale).</summary>
        public double Horde { get; set; } = 1;
        /// <summary>What is found (supplies) and paid (a special's reward), that share (RunFight.Loot).</summary>
        public double Loot { get; set; } = 1;
        /// <summary>Wounds carried home and out again (CampaignSoldier.Hurt): healed only at a Refuge, with medicines and the upgrade.</summary>
        public bool Wounds { get; set; }
    }

    public static class Levels
    {
        public static readonly IReadOnlyDictionary<string, LevelDef> All = new Dictionary<string, LevelDef>
        {
            [LevelIds.Easy] = new LevelDef { Id = LevelIds.Easy, Dead = 0.6, Horde = 0.5, Loot = 1.5 },
            [LevelIds.Medium] = new LevelDef { Id = LevelIds.Medium, Dead = 0.8, Horde = 0.75, Loot = 1.25 },
            [LevelIds.Hard] = new LevelDef { Id = LevelIds.Hard },
            [LevelIds.Survival] = new LevelDef { Id = LevelIds.Survival, Dead = 1.25, Horde = 1.3, Loot = 0.75, Wounds = true },
        };

        /// <summary>A game's level (hard when missing: every game made before).</summary>
        public static LevelDef Of(CampaignState c) => c.GameLevel != null && All.TryGetValue(c.GameLevel, out var def) ? def : All[LevelIds.Hard];

        /// <summary>
        /// A fight's groups of the dead for the level (RunFights.Launch): as many as its share makes of them, never fewer than
        /// one; the first ones kept where they were in the plan and the rest dropped, or past the plan's the first ones again
        /// at its end (as Setup.ScalePods). The living's groups as they are, where they were (Producto's review of 23/11: in
        /// front of them all, they changed which group went to which place).
        /// </summary>
        public static List<List<string>> Pods(List<List<string>> pods, double share)
        {
            if (share == 1 || pods.Count == 0) return pods;
            bool Dead(List<string> g) => g.Count > 0 && g.All((t) => Content.Templates[t].Undead);
            var dead = pods.Where(Dead).ToList();
            if (dead.Count == 0) return pods;
            var n = Math.Max(1, (int)Js.Round(dead.Count * share));
            var out_ = new List<List<string>>();
            var kept = 0;
            foreach (var g in pods)
            {
                if (!Dead(g)) out_.Add(g);
                else if (kept++ < n) out_.Add(g);
            }
            for (var i = dead.Count; i < n; i++) out_.Add(new List<string>(dead[i % dead.Count]));
            return out_;
        }
    }
}
