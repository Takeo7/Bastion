using Bastion.UI.App;
using UnityEngine;

namespace Bastion.UI.EditorTools
{
    /// <summary>
    /// How the frames are paced (GameSettings.Pacing) for every cap of Ajustes, out of the battle and in it, on the Mac and
    /// on Windows, with the telemetry's words (Perf.CapNote), against how they went before the Mac's battle went without
    /// sync (1/11, 0.11.4): only the Mac in the battle with no cap may change, to the screen's refresh. In the editor Apply
    /// leaves Unity's own as they are, so this is the check.
    ///
    ///   unity/tools/unity.sh -executeMethod Bastion.UI.EditorTools.FramePacing.Check
    /// </summary>
    public static class FramePacing
    {
        public static void Check()
        {
            var wrong = 0;
            foreach (var mac in new[] { false, true })
                foreach (var menu in new[] { true, false })
                    foreach (var cap in GameSettings.Caps)
                        foreach (var hz in new[] { 60, 120, 144, 0 })
                        {
                            var (sync, rate) = GameSettings.Pacing(cap, menu, mac, hz);
                            // Before: none, the screen's sync; a cap, that many frames and no sync; out of the battle, MenuFps at most.
                            var was = menu && (cap == 0 || cap > GameSettings.MenuFps) ? GameSettings.MenuFps : cap;
                            var before = was == 0 ? (1, -1) : (0, was);
                            var screen = mac && !menu && cap == 0;
                            var expected = screen ? (0, hz >= 30 ? hz : 60) : before;
                            var ok = (sync, rate) == expected;
                            if (!ok) wrong++;
                            var change = (sync, rate) == before ? "igual" : $"antes vsync {before.Item1}, límite {before.Item2}";
                            Debug.Log($"[fps] {(mac ? "Mac" : "Windows")} {(menu ? "menú" : "combate")}, tope {cap}, pantalla {hz} Hz: vsync {sync}, límite {rate} ·{Perf.CapNote(sync, hz, rate, screen)} · {change}{(ok ? "" : " · MAL")}");
                        }
            Debug.Log(wrong == 0 ? "[fps] bien" : $"[fps] {wrong} MAL");
        }
    }
}
