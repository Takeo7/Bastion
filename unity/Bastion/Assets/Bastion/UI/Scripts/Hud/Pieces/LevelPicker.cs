using System;
using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The game's level as four cards side by side (N9; cuadro 125, Arturo 23/11): each its name and what it changes, the one
    /// picked lit, Supervivencia's name in the danger red; over them «DIFICULTAD» with a lock (it isn't changed after), under
    /// them what goes for all. What it changes in figures, not sentences (U3, cuadro 157): the dead, the horde and the
    /// supplies as a share of Difícil's (Levels.All, so they follow the tuning), green where it helps, red where it doesn't;
    /// Supervivencia's wounds a line of their own. One piece for the start screen's new game and the base's «Nueva partida»
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
            var icons = Resources.Load<HudIconTable>("HudIcons");
            var head = Ui.El("level-pick__head", Root);
            Ui.Text(Lore.GameLevelText.Title.ToUpperInvariant(), "level-pick__label", head);
            // Chosen once (U3): a lock and three words, where the sentence was.
            Ui.Icon(icons != null ? icons.Get : (Func<string, Sprite>)null, "lock", "level-pick__lock", head);
            Ui.Text(HudText.GameLevel.Fixed, "level-pick__fixed", head);
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
                // Its three figures against Difícil's (100 %), and Supervivencia's wounds.
                var def = Levels.All[id];
                Row(key, icons, "level:dead", HudText.GameLevel.Dead, def.Dead, false);
                Row(key, icons, "noise", HudText.GameLevel.Horde, def.Horde, false);
                Row(key, icons, "supplies", HudText.GameLevel.Supplies, def.Loot, true);
                if (def.Wounds)
                {
                    var wounds = Ui.El("level-pick__row level-pick__row--worse", key);
                    wounds.pickingMode = PickingMode.Ignore;
                    Ui.Icon(icons != null ? icons.Get : (Func<string, Sprite>)null, "level:wounds", "level-pick__icon", wounds);
                    Ui.Text(HudText.GameLevel.Wounds, "level-pick__wounds", wounds).pickingMode = PickingMode.Ignore;
                }
                list.Add(key);
                keys[id] = key;
            }
            Ui.Text(HudText.GameLevel.AllLose, "level-pick__note", Root);
            Set(level);
        }

        /// <summary>
        /// A figure of a level's (U3): its icon, its word and its share of Difícil's, «60 %»; green where it helps the squad
        /// (fewer dead, a slower horde, more supplies), red where it doesn't, as it is at 100 %.
        /// </summary>
        static void Row(VisualElement card, HudIconTable icons, string icon, string word, double share, bool moreIsBetter)
        {
            var pct = (int)Math.Round(share * 100);
            var better = pct != 100 && (pct > 100) == moreIsBetter;
            var row = Ui.El("level-pick__row" + (pct == 100 ? "" : better ? " level-pick__row--better" : " level-pick__row--worse"), card);
            row.pickingMode = PickingMode.Ignore;
            if (icon == "supplies") Bastion.UI.Strategy.SuppliesMark.Crate(row, "level-pick__icon");
            else if (icon == "noise")
            {
                var mark = Ui.El("level-pick__icon", row);
                mark.pickingMode = PickingMode.Ignore;
                mark.Add(new LineIcon(icon));
            }
            else Ui.Icon(icons != null ? icons.Get : (Func<string, Sprite>)null, icon, "level-pick__icon", row);
            Ui.Text(word, "level-pick__word", row).pickingMode = PickingMode.Ignore;
            Ui.Text(pct + " %", "level-pick__pct font-title", row).pickingMode = PickingMode.Ignore;
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
