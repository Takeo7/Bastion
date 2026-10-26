using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.Client.Net
{
    /// <summary>
    /// For the tests' runs only (CampaignRun, BASTION_CAMPANA; Measure, BASTION_MEDIR): what a player would click on the
    /// screens, from outside them, without touching them. A button showing with a text is pressed as Intro presses it
    /// (a submit event); a base's tab is opened as its own button opens it (BaseSession.Control.OnTab, found by name:
    /// the screens are UI's, whose assembly refers to this one).
    /// </summary>
    internal static class UiPress
    {
        /// <summary>Presses every button showing whose text is `text`; how many.</summary>
        public static int Button(string text)
        {
            var pressed = 0;
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var root = doc.rootVisualElement;
                if (root == null) continue;
                // As Click: the base's buttons are in capitals («ENTENDIDO»), the combat's not.
                foreach (var b in root.Query<Button>().ToList().Where((x) => string.Equals(x.text, text, StringComparison.OrdinalIgnoreCase) && Showing(x)))
                {
                    using (var e = NavigationSubmitEvent.GetPooled())
                    {
                        e.target = b;
                        b.SendEvent(e);
                    }
                    pressed++;
                }
            }
            return pressed;
        }

        /// <summary>
        /// A click on every text showing that says `text` (any case): it goes up to what listens for it, as a player's
        /// click there (the turn's stamp, «clic para seguir»). How many.
        /// </summary>
        public static int Click(string text)
        {
            var clicked = 0;
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var root = doc.rootVisualElement;
                if (root == null) continue;
                foreach (var t in root.Query<TextElement>().ToList().Where((x) => string.Equals(x.text, text, StringComparison.OrdinalIgnoreCase) && Showing(x)))
                {
                    using (var e = ClickEvent.GetPooled())
                    {
                        e.target = t;
                        t.SendEvent(e);
                    }
                    clicked++;
                }
            }
            return clicked;
        }

        private static bool Showing(VisualElement e)
        {
            if (e.panel == null || !e.enabledInHierarchy || e.worldBound.width <= 0 || e.worldBound.height <= 0) return false;
            for (var x = e; x != null; x = x.parent)
                if (x.resolvedStyle.display == DisplayStyle.None || x.resolvedStyle.visibility == Visibility.Hidden || x.resolvedStyle.opacity <= 0) return false;
            return true;
        }

        /// <summary>The base's screen `view` (facilities, barracks, geoscape…), as its tab opens it; false if there is no base showing.</summary>
        public static bool Tab(string view)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select((a) => a.GetType("Bastion.UI.Strategy.BaseSession", false)).FirstOrDefault((t) => t != null);
            if (type == null) return false;
            var session = UnityEngine.Object.FindFirstObjectByType(type);
            var control = session != null ? type.GetProperty("Control")?.GetValue(session) : null;
            var open = control?.GetType().GetMethod("OnTab");
            if (open == null) return false;
            open.Invoke(control, new object[] { view });
            return true;
        }
    }
}
