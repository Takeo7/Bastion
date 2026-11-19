using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.UI.Hud;
using Bastion.UI.Strategy;
using UnityEditor;
using UnityEngine;

namespace Bastion.UI.EditorTools
{
    /// <summary>
    /// Every stage kind of the runs and every modifier has its icon (U26: El herido came out in the R10 as an empty ring, its
    /// «node:wounded» missing from the table). On every compile, unity.sh's too: each one's (StrategyPresenter.StageIcons)
    /// must be one of our line drawings (LineIcon) or have its sprite in Resources/HudIcons.asset; else an exception in the
    /// log, which unity.sh prints. A new one: its line in HudIcons.cs, then HudIcons.Make.
    /// </summary>
    static class NodeIconsCheck
    {
        const string TablePath = "Assets/Bastion/UI/Resources/HudIcons.asset";

        sealed class MissingNodeIconException : Exception
        {
            public MissingNodeIconException(string message) : base(message) { }
        }

        [InitializeOnLoadMethod]
        static void Check()
        {
            var missing = Missing();
            if (missing == null || missing.Count == 0) return;
            Debug.LogException(new MissingNodeIconException("Etapas sin icono en la tabla del HUD (HudIcons.cs, luego HudIcons.Make): " + string.Join(", ", missing)));
        }

        /// <summary>The kinds whose icon is nowhere, as «wounded (node:wounded)»; null without the table (a fresh import).</summary>
        public static List<string> Missing()
        {
            if (!File.Exists(TablePath)) return null;
            var drawn = Drawn(File.ReadAllLines(TablePath));
            return StrategyPresenter.StageIcons().Where(s => !LineIcon.Has(s.Icon) && !drawn.Contains(s.Icon)).Select(s => $"{s.Kind} ({s.Icon})").Distinct().ToList();
        }

        /// <summary>The table's ids with a sprite: «- Id: x» and, on the line under it, a «Sprite: {fileID: …}» other than 0.</summary>
        static HashSet<string> Drawn(string[] lines)
        {
            var ids = new HashSet<string>();
            for (var i = 0; i + 1 < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("- Id: ")) continue;
                var sprite = lines[i + 1].Trim();
                if (sprite.StartsWith("Sprite: {fileID:") && !sprite.StartsWith("Sprite: {fileID: 0}")) ids.Add(line.Substring(6).Trim().Trim('\'', '"'));
            }
            return ids;
        }
    }
}
