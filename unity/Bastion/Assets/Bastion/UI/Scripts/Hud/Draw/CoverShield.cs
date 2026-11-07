using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// A cover's shield on the soldier's panel (Arturo, 7/11: «el escudito correspondiente, del color correspondiente»):
    /// Synty INTERFACE Apocalypse HUD's shield (Image_Apocalypse_Icon's sprite, ICON_Apocalpyse_Status_Armor_01: Ana, 7/11
    /// #2), as the move's on each side with cover (MoveOverlay.ShowCover), in the colour the flag's square had: full, blue;
    /// half, yellow; flanked, red.
    /// </summary>
    public sealed class CoverShield : VisualElement
    {
        /// <summary>Flanked, half, full: the flag's colours (Flags.uss .uflag__cover--0, 1, 2).</summary>
        public static readonly Color[] Colours = { new Color32(0xf0, 0x50, 0x3c, 0xff), new Color32(0xff, 0xcf, 0x5a, 0xff), new Color32(0x7f, 0xd3, 0xff, 0xff) };

        int level = -1;

        /// <param name="sprite">The shield's sprite (the HUD's «stat:armor»); none, nothing drawn.</param>
        public CoverShield(Sprite sprite)
        {
            AddToClassList("cover-shield");
            pickingMode = PickingMode.Position;
            if (sprite != null) style.backgroundImage = new StyleBackground(sprite);
        }

        /// <summary>2 full, 1 half, 0 flanked.</summary>
        public int Level
        {
            get => level;
            set
            {
                level = value;
                style.unityBackgroundImageTintColor = Colours[Mathf.Clamp(value, 0, 2)];
                style.visibility = value < 0 ? Visibility.Hidden : Visibility.Visible;
            }
        }
    }
}
