using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The map from above (Arturo, 31/10 #12; 01/11: not a little map, the camera itself, straight down and far enough
    /// for all of it): while the camera is up there, a strip under the turn's that says what it is and what to do.
    /// </summary>
    public sealed class MiniMap
    {
        readonly VisualElement host;

        public bool Open { get; private set; }

        public MiniMap(VisualElement host)
        {
            this.host = host;
            host.pickingMode = PickingMode.Ignore;
            var strip = Ui.El("mini-map__strip panel", host);
            strip.pickingMode = PickingMode.Ignore;
            Ui.Text(HudText.MiniMap.Title, "mini-map__title font-title", strip);
            Ui.Text(HudText.MiniMap.Hint, "mini-map__hint", strip);
            Ui.Show(host, false);
        }

        public void Set(bool open)
        {
            Open = open;
            Ui.Show(host, open);
        }
    }
}
