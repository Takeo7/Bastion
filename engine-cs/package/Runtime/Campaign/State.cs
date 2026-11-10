#nullable enable
// campaign.ts: what changes the state in small steps (paying, the log, ids, a new soldier, a soldier who turns, the
// rations coming in, a weapon's wear coming home), and the clock's own intervals.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // -------------------------------------------------------------------- state

        /// <summary>Days (missions) between the enemy clock's steps: 12 days of before were 2.4 missions.</summary>
        private const int DoomInterval = 2;
        // A mission takes no days (playtest 2026-10-14, docs/PRUEBA-2026-10-14.md P7):
        // time passes waiting for news, not flying out. Before, La Lata's trip took
        // 2 days and one more a road stretch (docs/FASE5.md).
        /// <summary>Least days between one story chapter won and the next one opening.</summary>
        private const int StoryGap = 2;
        private const int FacilityDoomInterval = 3;
        private const int MaxFacilities = 3;
        private const int MaxOffers = 3;
        /// <summary>Days (missions) between Bielsa's convoys.</summary>
        private const int Month = 6;

        /// <summary>Credits for each alien body sold after a won mission.</summary>
        private static readonly IReadOnlyDictionary<string, int> BodyValue = new Dictionary<string, int>
        {
            [TemplateIds.Trooper] = 2,
            [TemplateIds.Officer] = 4,
            [TemplateIds.Lancer] = 3,
            [TemplateIds.Xenoid] = 4,
            [TemplateIds.Mec] = 6,
            [TemplateIds.Sectoid] = 5,
            [TemplateIds.Bloater] = 3,
        };

        // ------------------------------------------------------------------ helpers

        private static void Pay(CampaignState c, int cost)
        {
            c.Credits -= cost;
        }

        private static void Log(CampaignState c, string text, string tone = LogTones.Info, LogAlert? alert = null)
        {
            c.Log.Insert(0, new LogEntry { Day = c.Day, Text = text, Tone = tone, Alert = alert });
            if (c.Log.Count > 40) c.Log.RemoveRange(40, c.Log.Count - 40);
        }

        private static LogAlert Alert(string kicker, string title, string? rumor = null)
        {
            return new LogAlert { Kicker = kicker, Title = title, Rumor = rumor };
        }

        private static string NewId(CampaignState c, string prefix)
        {
            return $"{prefix}{++c.NextId}";
        }

        /// <summary>A name nobody in the campaign has yet, with the sex it goes with.</summary>
        private static (string Name, string Sex) SoldierName(CampaignState c, Rng rng)
        {
            var used = new HashSet<string>(c.Soldiers.Select((s) => s.Name));
            var free = Content.SoldierNames.Where((n) => !used.Contains(n.Name)).ToList();
            if (free.Count > 0)
            {
                var n = rng.Pick(free);
                return (n.Name, n.Sex);
            }
            var any = rng.Pick(Content.SoldierNames);
            return ($"{any.Name} {c.Soldiers.Count + 1}", any.Sex);
        }

        /// <summary>
        /// A new soldier (docs/FASE10.md): no class, a bar filled in one or two lines
        /// at random, so no two are alike, and its points still to spend.
        /// </summary>
        /// <remarks>
        /// `armed`: one of StartArms at random, as those who start had (Arturo, 5/11: «los nuevos reclutas reciben un arma
        /// inicial aleatoria como los del inicio»): a pistol half the time, a machete or a crowbar a quarter each. Only where
        /// the campaign counts its pistols (CampaignState.Pistols); never at the start, which deals its own.
        /// </remarks>
        private static CampaignSoldier AddSoldier(CampaignState c, int owner, Rng rng, bool armed = true)
        {
            var (name, sex) = SoldierName(c, rng);
            var id = NewId(c, "c");
            var lines = Bars.RecruitSkills(rng);
            var s = new CampaignSoldier
            {
                Id = id,
                Name = name,
                Owner = owner,
                Kills = 0,
                Missions = 0,
                Skills = lines.Skills,
                Growth = lines.Growth,
                Board = new List<string>(),
                WoundedDays = 0,
                Alive = true,
                Loadout = new Loadout(),
                Nickname = "",
                Appearance = Appearances.DefaultAppearance(owner, null, sex, id, Appearances.Wardrobe),
                Traits = new List<string>(),
            };
            c.Soldiers.Add(s);
            if (armed && c.Pistols == true) (c.FoundWeapons ??= new List<FoundWeapon>()).Add(new FoundWeapon { Id = NewId(c, "w"), Kind = rng.Pick(StartArms), Holder = s.Id });
            // In a siege every soldier fit defends (docs/FASE12.md §11.5): one who comes once it has started, too (Red, 2/11).
            if (ActiveOffer(c)?.Siege == true)
            {
                c.Squad.Add(s.Id);
                SyncPads(c);
            }
            return s;
        }

        /// <summary>
        /// A commander left without a soldier alive gets a recruit for nothing (Arturo, 2/11: with two each, a bad mission
        /// can leave nobody to go out), at the base at once. True if anyone came.
        /// </summary>
        private static bool NobodyLeft(CampaignState c, Rng rng)
        {
            if (c.Outcome != null) return false;
            var came = false;
            foreach (var owner in c.Players)
            {
                if (c.Soldiers.Any((s) => s.Owner == owner && s.Alive)) continue;
                var s = AddSoldier(c, owner, rng);
                // Whose it is, without naming a side: left and right are each player's own (Lore, 2/11).
                Log(c, $"{s.Name} se une al Bastión como recluta: {(c.Players.Count > 1 ? "a un jugador no le quedaban" : "no quedaban")} soldados vivos.", LogTones.Good);
                came = true;
            }
            return came;
        }

        /// <summary>The player with the fewest soldiers gets newcomers.</summary>
        private static int NeediestPlayer(CampaignState c)
        {
            var owners = c.Players.Select((slot) => (Slot: slot, Count: c.Soldiers.Count((s) => s.Owner == slot && s.Alive))).ToList();
            Js.Sort(owners, (a, b) => a.Count - b.Count);
            return owners[0].Slot;
        }

        /// <summary>«a, b y c».</summary>
        private static string ListOf(IReadOnlyList<string> parts)
        {
            return parts.Count > 1 ? $"{string.Join(", ", parts.Take(parts.Count - 1))} y {parts[parts.Count - 1]}" : parts.Count > 0 ? parts[0] : "";
        }

        /// <summary>`rng.int(2 ** 31)`: a map's seed. 2^31 is past an int, so as rng.ts does it (Room.cs too).</summary>
        private static int Seed31(Rng rng)
        {
            return (int)Math.Floor(rng.Next() * 2147483648.0);
        }

        /// <summary>A number in base 36, as JavaScript's toString(36) writes a whole one that isn't negative.</summary>
        private static string Base36(int n)
        {
            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (n == 0) return "0";
            var text = "";
            for (var v = n; v > 0; v /= 36) text = digits[v % 36] + text;
            return text;
        }

        // ------------------------------------------------------------------- food

        /// <summary>Rations come in; with enough for a day again, the hunger is over.</summary>
        private static void AddFood(CampaignState c, int n)
        {
            if (n <= 0) return;
            c.Food = (c.Food ?? 0) + n;
            if (c.Hungry == true && c.Food >= DailyRations(c))
            {
                c.Hungry = false;
                Log(c, "Vuelve a haber raciones: se acabó el hambre.", LogTones.Good);
            }
        }

        // --------------------------------------------- the Armería (docs/FASE9.md §5)

        /// <summary>
        /// A weapon's condition and mods as they came back (docs/FASE9.md §5), for its FoundWeapon: `Object.assign(w,
        /// wornAs(…))`, which sets what came and leaves the rest.
        /// </summary>
        private static void WearAs(FoundWeapon w, double? condition, IReadOnlyList<string>? mods)
        {
            if (condition != null) w.Condition = condition;
            if (mods != null && mods.Count > 0) w.Mods = new List<string>(mods);
        }

        // ------------------------------------------------------------- the soldiers

        /// <summary>A newcomer who has been out there before: `steps` bars more, in a line at random each.</summary>
        private static void Seasoned(CampaignSoldier s, int steps, Rng rng)
        {
            for (var k = 0; k < steps; k++)
            {
                var more = Bars.OneMoreBar(s.Skills, s.Growth, rng);
                s.Skills = more.Skills;
                s.Growth = more.Growth;
            }
        }
    }
}
