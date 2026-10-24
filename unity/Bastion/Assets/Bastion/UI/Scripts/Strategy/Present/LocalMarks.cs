using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// What the 0.10.1 kept in the browser, per player and campaign, kept on this computer (PlayerPrefs; with
    /// BASTION_DATOS apart, as its ?perfil): the «Nuevo» marks (fresh.ts) and the news already seen (seenNews).
    /// </summary>
    public static class LocalMarks
    {
        /// <summary>bastion.&lt;what&gt;.&lt;campaign&gt;, and the data folder's when BASTION_DATOS gives one (two apps on one computer).</summary>
        public static string Key(string what, CampaignState c)
        {
            var campaign = c.Uid ?? string.Join("|", c.Soldiers.Take(2).Select(s => s.Name));
            var profile = Environment.GetEnvironmentVariable("BASTION_DATOS");
            return $"bastion.{what}.{campaign}" + (string.IsNullOrEmpty(profile) ? "" : "." + Hash(profile));
        }

        public static List<string> Read(string key) =>
            PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList() : null;

        public static void Write(string key, IEnumerable<string> items)
        {
            PlayerPrefs.SetString(key, string.Join("\n", items));
            PlayerPrefs.Save();
        }

        static string Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var ch in s) h = (h ^ ch) * 16777619;
                return h.ToString("x8");
            }
        }
    }

    /// <summary>
    /// What each level of the Bastion brings, and which of it this player hasn't looked at yet (fresh.ts; playtest 20/10
    /// #13): «Nuevo» where it lives (a piece of the palette, a building's upgrade on its card, a belt item, an armour or
    /// a work in the Armería) until the pointer goes over it. A campaign first opened here starts with nothing new: only
    /// what the levels bought from now on bring.
    /// </summary>
    public sealed class FreshMarks
    {
        /// <summary>Where a thing a level brings is found: the Asentamiento (facilities) or the Armería (engineering).</summary>
        public sealed class Thing
        {
            public string Key;
            public string View;
            /// <summary>A building's upgrade: news only where there is one of that building to raise to it.</summary>
            public string RaisePiece;
            public int RaiseTo;
        }

        string key;
        int from = 1;
        readonly HashSet<string> seen = new HashSet<string>();

        /// <summary>The Bastion level a work of the Taller needs: its technology's, else its own (craftLevel).</summary>
        public static int CraftLevel(CraftDef def) => def.Tech != null ? Progression.Techs[def.Tech].Level : def.Level ?? 1;

        /// <summary>The armour tiers by the Armería's row keys, and the level each needs (ARMOR_LEVELS).</summary>
        public static readonly IReadOnlyDictionary<string, int> ArmorLevels = new Dictionary<string, int>
        {
            ["a1"] = 1, ["a2"] = Progression.Techs[TechIds.PlateArmor].Level, ["a3"] = Progression.Techs[TechIds.PredatorArmor].Level,
        };

        /// <summary>Everything `level` brings, keyed as the screens mark it (levelThings).</summary>
        public static List<Thing> LevelThings(int level)
        {
            var out_ = new List<Thing>();
            foreach (var id in Base.PieceOrder)
            {
                var def = Base.Pieces[id];
                if (id == PieceIds.Caravan) continue;
                if (def.Level == level) out_.Add(new Thing { Key = $"piece:{id}", View = "facilities" });
                if (def.Kind == PieceKinds.Building)
                    foreach (var to in new[] { 2, 3 })
                        if (Progression.UpgradeLevel(def.Level, to) == level) out_.Add(new Thing { Key = $"raise:{id}:{to}", View = "facilities", RaisePiece = id, RaiseTo = to });
            }
            // Only the belt's: the Armería shows nothing else (Ana, 23/10 #39).
            foreach (var item in Content.Items.Values) if (item.Level == level && item.Slot == ItemSlots.Utility) out_.Add(new Thing { Key = $"item:{item.Id}", View = "engineering" });
            foreach (var pair in ArmorLevels) if (pair.Value == level && pair.Value > 1) out_.Add(new Thing { Key = $"armor:{pair.Key}", View = "engineering" });
            foreach (var def in Progression.Crafts.Values) if (def.Kind != CraftKinds.Armor && CraftLevel(def) == level && level > 1) out_.Add(new Thing { Key = $"craft:{def.Id}", View = "engineering" });
            return out_;
        }

        /// <summary>This campaign's marks, read once.</summary>
        public void Load(CampaignState c)
        {
            var k = LocalMarks.Key("fresh", c);
            if (k == key) return;
            key = k;
            seen.Clear();
            var stored = LocalMarks.Read(k);
            var level = Progression.BastionLevel(c.Level);
            from = stored != null && stored.Count > 0 && int.TryParse(stored[0], out var f) ? f : level;
            if (stored != null) foreach (var s in stored.Skip(1)) seen.Add(s);
            else Save();
        }

        /// <summary>What is new up to `level`, not looked at yet.</summary>
        public List<Thing> Fresh(int level)
        {
            var out_ = new List<Thing>();
            for (var n = from + 1; n <= level; n++) out_.AddRange(LevelThings(n).Where(t => !seen.Contains(t.Key)));
            return out_;
        }

        public bool IsFresh(string thing, int level) => Fresh(level).Any(t => t.Key == thing);

        /// <summary>Looked at: its «Nuevo» goes. Whether it was new.</summary>
        public bool See(string thing)
        {
            if (key == null || !seen.Add(thing)) return false;
            Save();
            return true;
        }

        void Save() => LocalMarks.Write(key, new[] { from.ToString() }.Concat(seen));
    }

    /// <summary>
    /// The news of each screen this player has seen (screen.ts navNews, seeNews): a tab's number counts only what is new
    /// to them, and goes once the screen is visited. A campaign first opened here starts with nothing new.
    /// </summary>
    public sealed class NewsMarks
    {
        string key;
        readonly HashSet<string> seen = new HashSet<string>();

        public void Load(CampaignState c, IEnumerable<string> now)
        {
            var k = LocalMarks.Key("seenNews", c);
            if (k == key) return;
            key = k;
            seen.Clear();
            var stored = LocalMarks.Read(k);
            foreach (var s in stored ?? now) seen.Add(s);
            if (stored == null) LocalMarks.Write(key, seen);
        }

        public bool Seen(string news) => seen.Contains(news);

        /// <summary>The screen's news, all seen; the keys of things long gone are dropped, so the list doesn't grow forever.</summary>
        public void See(IEnumerable<string> news, IEnumerable<string> live)
        {
            if (key == null) return;
            var added = false;
            foreach (var n in news) added |= seen.Add(n);
            if (!added) return;
            var alive = new HashSet<string>(live);
            seen.RemoveWhere(s => !alive.Contains(s));
            LocalMarks.Write(key, seen);
        }
    }
}
