using System;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The stamp in the middle (hud.ts banner; docs/UNITY-UI.md §2.5): «TU TURNO», «TURNO ENEMIGO», «¡CONTACTO!»… with a
    /// line under it, coming down like a rubber stamp (and its sound): as Mixta drew it (mixta-hud.css .banner), a plate of
    /// dark steel between two hazard bands, yellow or red, its words straight; not the paper stamp under it (styles.css),
    /// which read badly over the snow (Ana, 28/10). It goes by itself, or, `held`, stays until it is
    /// clicked, or Intro or Esc pressed, or the player does anything else (Release); meanwhile it covers the field and
    /// the turn clock waits (Covering).
    /// </summary>
    public sealed class Banner
    {
        const long ShortMs = 1500;
        const long LongMs = 3600;

        readonly VisualElement host;
        readonly IHudHandlers handlers;
        readonly Action<string> sound;
        IVisualElementScheduledItem timer;
        bool held;

        public Banner(VisualElement host, IHudHandlers handlers, Action<string> sound)
        {
            this.host = host;
            this.handlers = handlers;
            this.sound = sound;
            host.pickingMode = PickingMode.Ignore;
            host.RegisterCallback<ClickEvent>(_ => Release());
        }

        /// <summary>Held and up: it covers the field to be read.</summary>
        public bool Covering => held && host.ClassListContains("banner--show");

        /// <summary>kind: xcom, alien or warn.</summary>
        public void Show(string text, string kind = "xcom", string detail = null, bool hold = false)
        {
            sound?.Invoke("stamp");
            timer?.Pause();
            host.Clear();
            var plate = Ui.El("banner__plate", host);
            Ui.El("banner__hazard banner__hazard--top", plate);
            Ui.Text(text, "banner__text font-title", plate);
            if (!string.IsNullOrEmpty(detail)) Ui.Text(detail, "banner__detail", plate);
            if (hold) Ui.Text(HudText.Banner.Go.ToUpperInvariant(), "banner__go", plate);
            Ui.El("banner__hazard banner__hazard--bottom", plate);
            // The clicks on the plate, the host's (held) or none.
            plate.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
            foreach (var k in new[] { "xcom", "alien", "warn" }) host.EnableInClassList("banner--" + k, kind == k);
            held = hold;
            // Clickable only while held; otherwise the clicks go through to the map.
            host.pickingMode = hold ? PickingMode.Position : PickingMode.Ignore;
            // From big and faint to its size: the class goes on a frame later, so the transition runs from the start.
            host.RemoveFromClassList("banner--show");
            host.schedule.Execute(() => host.AddToClassList("banner--show"));
            if (!hold) timer = host.schedule.Execute(Hide).StartingIn(string.IsNullOrEmpty(detail) ? ShortMs : LongMs);
        }

        /// <summary>A held stamp let go: by its click, Intro or Esc; `quiet`, by doing anything else, without its sound. Whether there was one.</summary>
        public bool Release(bool quiet = false)
        {
            if (!Covering) return false;
            Hide();
            if (!quiet) sound?.Invoke("confirm");
            handlers.OnBannerReleased();
            return true;
        }

        void Hide()
        {
            held = false;
            host.pickingMode = PickingMode.Ignore;
            host.RemoveFromClassList("banner--show");
        }
    }
}
