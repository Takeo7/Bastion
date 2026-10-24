using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// When each of the story's radio scenes plays (strategy/transmissions.ts Transmissions): call Update on every change
    /// of the campaign. A new day (a mission flown, a day waited) or the first look at a campaign is a moment; one scene
    /// a moment at most, the urgent ones at once (Transmissions.NextScene); `hold` (the debrief) keeps it for later.
    /// Each computer remembers the scenes it has played, per campaign (PlayerPrefs; with BASTION_DATOS, apart, as the
    /// 0.10.1's ?perfil). The first time it sees a campaign, what already happened counts as heard; in its first five days
    /// the welcome still plays, later a recap of where things stand plays instead.
    /// </summary>
    public sealed class Radio
    {
        readonly RadioPanel panel;
        readonly int slot;
        readonly Func<Dictionary<string, string>> vars;
        string key = "";
        HashSet<string> seen = new HashSet<string>();
        int? lastDay;
        bool moment;
        bool recap;

        /// <summary>The recap's lines for `slot` (the engine's, Transmissions.RecapLines); null, no recap.</summary>
        public Func<CampaignState, int, List<SceneLine>> RecapOf = Transmissions.RecapLines;

        /// <summary>`vars`: the placeholders' values (saludo, yo: StrategyText.Radio.Greeting); `slot`: this player's seat.</summary>
        public Radio(RadioPanel panel, int slot, Func<Dictionary<string, string>> vars)
        {
            this.panel = panel;
            this.slot = slot;
            this.vars = vars;
        }

        public void Update(CampaignState c, bool hold = false)
        {
            if (c == null) return;
            Load(c);
            if (lastDay != c.Day)
            {
                lastDay = c.Day;
                moment = true;
            }
            if (hold || panel.Active) return;
            if (recap)
            {
                recap = false;
                moment = false;
                var lines = RecapOf?.Invoke(c, slot);
                if (lines != null && lines.Count > 0) panel.Play(View(Transmissions.Recap, lines));
                return;
            }
            var scene = Transmissions.NextScene(c, seen, moment);
            moment = false;
            if (scene == null) return;
            seen.Add(scene.Id);
            Save();
            panel.Play(View(scene, Transmissions.SceneLines(scene, c)));
        }

        /// <summary>A scene's lines as the card shows them: who by name and role, the placeholders filled.</summary>
        public RadioView View(RadioScene scene, IEnumerable<SceneLine> lines)
        {
            var values = vars?.Invoke() ?? new Dictionary<string, string>();
            string Fill(string text) => Regex.Replace(text ?? "", @"\{(\w+)\}", m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
            var v = new RadioView { Scene = scene.Id, Title = scene.Title, Channel = scene.Channel };
            foreach (var l in lines)
            {
                var who = Transmissions.Speakers.TryGetValue(l.Who, out var s) ? s : new RadioSpeaker { Name = l.Who };
                v.Lines.Add(new RadioLineView { Who = l.Who, Name = who.Name, Role = who.Role, Text = Fill(l.Text), Rule = l.Rule });
            }
            return v;
        }

        void Load(CampaignState c)
        {
            var k = StoreKey(c);
            if (k == key) return;
            key = k;
            lastDay = null;
            var stored = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k, "") : null;
            seen = stored != null
                ? new HashSet<string>(stored.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                : new HashSet<string>(Transmissions.UnlockedScenes(c).Select(s => s.Id));
            recap = stored == null && c.Day > 5;
            if (stored == null) Save();
        }

        void Save()
        {
            PlayerPrefs.SetString(key, string.Join(",", seen));
            PlayerPrefs.Save();
        }

        /// <summary>bastion.scenes.&lt;campaign&gt;, and the data folder's when BASTION_DATOS gives one (two apps on one computer).</summary>
        static string StoreKey(CampaignState c)
        {
            var campaign = c.Uid ?? string.Join("|", c.Soldiers.Take(2).Select(s => s.Name));
            var profile = Environment.GetEnvironmentVariable("BASTION_DATOS");
            return "bastion.scenes." + campaign + (string.IsNullOrEmpty(profile) ? "" : "." + Hash(profile));
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
}
