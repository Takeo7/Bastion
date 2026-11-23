using Bastion.UI.App;
using UnityEngine;

namespace Bastion.UI.EditorTools
{
    /// <summary>
    /// How the frames are paced (GameSettings.Pacing) for every cap of Ajustes, out of the battle and in it, with the
    /// screen's sync on and off (N1, 22/11), with the telemetry's words (Perf.CapNote). What must hold: never without a cap
    /// and without the sync (the Mac's battle hung so with the screen asleep, 1/11); with the sync, no fewer frames than
    /// the cap asks, nor than the screen gives (a frame every so many refreshes); without it, Ajustes' cap or the screen's refresh. In the editor Apply
    /// leaves Unity's own as they are, so this is the check.
    ///
    ///   unity/tools/unity.sh -executeMethod Bastion.UI.EditorTools.FramePacing.Check
    /// </summary>
    public static class FramePacing
    {
        public static void Check()
        {
            var wrong = 0;
            foreach (var vsync in new[] { true, false })
                foreach (var menu in new[] { true, false })
                    foreach (var cap in GameSettings.Caps)
                        foreach (var hz in new[] { 60, 120, 144, 0 })
                        {
                            var (sync, rate) = GameSettings.Pacing(cap, menu, hz, vsync);
                            var asked = menu && (cap == 0 || cap > GameSettings.MenuFps) ? GameSettings.MenuFps : cap;
                            bool ok;
                            if (sync > 0) ok = vsync && hz >= 30 && rate == -1 && (asked == 0 ? sync == 1 : hz / sync >= System.Math.Min(asked, hz));
                            else ok = rate > 0 && rate == (asked > 0 ? asked : hz >= 30 ? hz : 60);
                            if (!ok) wrong++;
                            var screen = sync == 0 && cap == 0 && !menu;
                            Debug.Log($"[fps] {(vsync ? "con" : "sin")} sincronía, {(menu ? "menú" : "combate")}, tope {cap}, pantalla {hz} Hz: vsync {sync}, límite {rate} ·{Perf.CapNote(sync, hz, rate, screen)}{(ok ? "" : " · MAL")}");
                        }
            Debug.Log(wrong == 0 ? "[fps] bien" : $"[fps] {wrong} MAL");
        }
    }
}
