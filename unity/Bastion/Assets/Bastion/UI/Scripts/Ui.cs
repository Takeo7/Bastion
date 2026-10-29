using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI
{
    /// <summary>
    /// Small helpers to build the HUD in code, as ui/dom.ts h() did on the web: elements with their classes, a line of
    /// pips, a template from Resources/Hud, and the key a view is compared by (JSON, as hud.ts compared its views).
    /// </summary>
    public static class Ui
    {
        /// <summary>
        /// A weapon's picture among the HUD's icons (HudIcons): the guns', and the frying pan's, the bat's, the crowbar's
        /// and the chainsaw's found in the field (Ana's notes, Arturo 29/10); a blade's for the knife, the machete, the rest.
        /// </summary>
        public static string WeaponIcon(string weapon)
        {
            switch (weapon)
            {
                case Bastion.Engine.WeaponIds.Rifle: case Bastion.Engine.WeaponIds.Pistol: case Bastion.Engine.WeaponIds.Shotgun:
                case Bastion.Engine.WeaponIds.Sniper: case Bastion.Engine.WeaponIds.Cannon: case Bastion.Engine.WeaponIds.Pan:
                case Bastion.Engine.WeaponIds.Bat: case Bastion.Engine.WeaponIds.Crowbar: case Bastion.Engine.WeaponIds.Chainsaw:
                    return "weapon:" + weapon;
                default:
                    return "overwatchMelee";
            }
        }

        /// <summary>A plain element with `classes` (space-separated), added to `parent` if given.</summary>
        public static VisualElement El(string classes = null, VisualElement parent = null)
        {
            var e = new VisualElement();
            AddClasses(e, classes);
            parent?.Add(e);
            return e;
        }

        /// <summary>A label with its text and `classes`, added to `parent` if given.</summary>
        public static Label Text(string text, string classes = null, VisualElement parent = null)
        {
            var l = new Label(text ?? "");
            AddClasses(l, classes);
            parent?.Add(l);
            return l;
        }

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) e.AddToClassList(c);
        }

        /// <summary>Shown (flex) or out of the layout altogether.</summary>
        public static void Show(VisualElement e, bool on)
        {
            e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>An icon (a sprite of the HUD's table) with `classes`; without a sprite, an empty box of its size.</summary>
        public static VisualElement Icon(System.Func<string, Sprite> icons, string id, string classes = null, VisualElement parent = null)
        {
            var e = El("ui-icon " + (classes ?? ""), parent);
            e.pickingMode = PickingMode.Ignore;
            var sprite = icons?.Invoke(id);
            if (sprite != null) e.style.backgroundImage = new StyleBackground(sprite);
            return e;
        }

        /// <summary>`total` pips in `row`, the first `filled` of them lit (health, actions, rounds).</summary>
        public static void Pips(VisualElement row, int filled, int total)
        {
            row.Clear();
            for (var i = 0; i < total; i++) El(i < filled ? "pip pip--on" : "pip", row);
        }

        /// <summary>A template of Resources/Hud, cloned (SquadCard, AbilityButton…).</summary>
        public static TemplateContainer Clone(string template)
        {
            var asset = Resources.Load<VisualTreeAsset>("Hud/" + template);
            if (asset == null)
            {
                Debug.LogError($"HUD: no template Hud/{template} in Resources");
                return new TemplateContainer();
            }
            return asset.Instantiate();
        }

        /// <summary>
        /// A template's elements put straight into `parent`, without the TemplateContainer that wraps a clone: so the
        /// parent's rules for its children (`.shot-panel > *`) reach them.
        /// </summary>
        public static void CloneInto(string template, VisualElement parent)
        {
            var tree = Clone(template);
            var children = new List<VisualElement>(tree.Children());
            foreach (var c in children) parent.Add(c);
        }

        /// <summary>What a view is compared by: its JSON (hud.ts drew a piece again only when this changed).</summary>
        public static string Key(object view)
        {
            return view == null ? "" : JsonUtility.ToJson(view);
        }

        /// <summary>A colour from «#rrggbb» (the players' colours come as such from the server).</summary>
        public static Color Hex(string hex, Color fallback)
        {
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        /// <summary>«a, b y c».</summary>
        public static string List(IList<string> items)
        {
            if (items.Count == 0) return "";
            if (items.Count == 1) return items[0];
            var head = new string[items.Count - 1];
            for (var i = 0; i < head.Length; i++) head[i] = items[i];
            return string.Join(", ", head) + " y " + items[items.Count - 1];
        }
    }
}
