using System;
using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The game's level as four cards side by side (N9; cuadro 125, Arturo 23/11): each its name and what it changes (Lore's
    /// GameLevelText), the one picked lit, Supervivencia's name in the danger red; over them «DIFICULTAD», under them that it
    /// isn't changed after and what goes for all. One piece for the start screen's new game and the base's «Nueva partida»
    /// after a game lost (Producto, 23/11: they were two). Its sheet, Hud/LevelPicker.uss, it brings itself.
    /// </summary>
    public sealed class LevelPicker
    {
        public readonly VisualElement Root;
        readonly Dictionary<string, Button> keys = new Dictionary<string, Button>();

        /// <summary>A card clicked: its level (LevelIds).</summary>
        public event Action<string> Picked;

        public string Level { get; private set; }

        public LevelPicker(string level)
        {
            Root = new VisualElement { name = "levels" };
            Root.AddToClassList("level-pick");
            var sheet = Resources.Load<StyleSheet>("Hud/LevelPicker");
            if (sheet != null) Root.styleSheets.Add(sheet);
            Ui.Text(Lore.GameLevelText.Title.ToUpperInvariant(), "level-pick__label", Root);
            var list = Ui.El("level-pick__list", Root);
            foreach (var id in LevelIds.All)
            {
                var key = new Button(() =>
                {
                    Set(id);
                    Picked?.Invoke(id);
                });
                key.AddToClassList("level-pick__card");
                if (id == LevelIds.Survival) key.AddToClassList("level-pick__card--hard");
                Ui.Text(Lore.GameLevelText.Names[id].ToUpperInvariant(), "level-pick__name font-title", key).pickingMode = PickingMode.Ignore;
                Ui.Text(Lore.GameLevelText.Lines[id], "level-pick__line", key).pickingMode = PickingMode.Ignore;
                list.Add(key);
                keys[id] = key;
            }
            Ui.Text(Lore.GameLevelText.Fixed, "level-pick__note", Root);
            Ui.Text(Lore.GameLevelText.AllLose, "level-pick__note", Root);
            Set(level);
        }

        /// <summary>The cards, as buttons (the start screen rests them while it waits).</summary>
        public IEnumerable<Button> Keys => keys.Values;

        public void Set(string level)
        {
            Level = level != null && keys.ContainsKey(level) ? level : LevelIds.Medium;
            foreach (var (k, key) in keys) key.EnableInClassList("level-pick__card--on", k == Level);
        }
    }
}
