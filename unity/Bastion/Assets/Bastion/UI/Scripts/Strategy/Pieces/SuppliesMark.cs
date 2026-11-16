using Bastion.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// An amount of supplies on screen: its figure and Arte's crate beside it (GearPictures' «supplies», the stash's pile
    /// small), not the «§» of the fonts, which looked like a banknote (Gameplay, 16/11: Arturo doesn't want money).
    /// </summary>
    public static class SuppliesMark
    {
        /// <summary>`amount` with the crate after it, into `parent`; `classes` on the row (supplies-mark--small: 16 points).</summary>
        public static VisualElement Make(VisualElement parent, string amount, string classes = null)
        {
            var row = Ui.El("supplies-mark " + (classes ?? ""), parent);
            Ui.Text(amount ?? "", "supplies-mark__value font-title", row);
            Crate(row);
            return row;
        }

        /// <summary>The crate alone, as an icon.</summary>
        public static VisualElement Crate(VisualElement parent, string classes = null)
        {
            var pic = Ui.El("supplies-mark__pic " + (classes ?? ""), parent);
            pic.pickingMode = PickingMode.Ignore;
            if (GearPictures.Of("supplies", null) is Texture2D t) pic.style.backgroundImage = new StyleBackground(t);
            return pic;
        }
    }
}
