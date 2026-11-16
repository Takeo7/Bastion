using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// For the finger (GameInput.OverUi; docs/IOS.md, phase 2): whether a screen point is on a panel of any screen (the
    /// HUD, a menu, a window), by the same rule as GameCursor's: a picked element that is not a screen's root nor its
    /// own layer. A finger that comes down there is the panel's (UI Toolkit presses its button, scrolls its list), and
    /// the map never sees it. Only where the game is played with the finger.
    /// </summary>
    static class FingerPanels
    {
        /// <summary>How often the screens' panels are looked for again, seconds (they come and go with the scenes).</summary>
        const float Look = 1f;

        static readonly List<IPanel> panels = new List<IPanel>();
        static float panelsAt = -10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (Bastion.Client.GameInput.Touch) Bastion.Client.GameInput.OverUi = Over;
        }

        /// <summary>Whether a screen point (y up) is on a panel of any screen.</summary>
        static bool Over(Vector2 at)
        {
            if (Time.unscaledTime - panelsAt > Look)
            {
                panelsAt = Time.unscaledTime;
                panels.Clear();
                foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                {
                    var panel = doc.rootVisualElement?.panel;
                    if (panel != null && !panels.Contains(panel)) panels.Add(panel);
                }
            }
            foreach (var panel in panels)
            {
                if (panel?.visualTree == null) continue;
                var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(at.x, Screen.height - at.y)));
                if (picked == null || picked == panel.visualTree || picked.parent == panel.visualTree || picked.pickingMode != PickingMode.Position) continue;
                return true;
            }
            return false;
        }
    }
}
