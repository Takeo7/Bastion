using System;
using System.IO;
using UnityEngine;

namespace Bastion.UI.App
{
    /// <summary>
    /// F12: a capture of the screen as it is, kept in Imágenes/Bastión (Red, 28/10: macOS's own Cmd+Mayús+4 reached the
    /// game too). Taken at the end of the frame, with the panels on it, and written here, so that whether it was kept is
    /// known and said (Lore's words). BASTION_CAPTURAS: another folder, for the tests.
    /// </summary>
    public static class Screenshots
    {
        public const string Saved = "Captura guardada en Imágenes/Bastión.";
        public const string Failed = "No se ha podido guardar la captura.";

        static bool taking;

        /// <summary>The folder the captures go to: Imágenes/Bastión, or BASTION_CAPTURAS.</summary>
        public static string Folder =>
            Environment.GetEnvironmentVariable("BASTION_CAPTURAS") is string test && test != ""
                ? test
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Bastión");

        /// <summary>A capture at the end of this frame; `say` gets what to tell the player (kept, or not).</summary>
        public static async void Take(Action<string> say)
        {
            if (taking) return;
            taking = true;
            try
            {
                // (A batch run draws no screen, so its frames never end there: the next one instead, for the tests.)
                if (Application.isBatchMode) await Awaitable.NextFrameAsync();
                else await Awaitable.EndOfFrameAsync();
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                var png = shot.EncodeToPNG();
                UnityEngine.Object.Destroy(shot);
                Directory.CreateDirectory(Folder);
                var file = Path.Combine(Folder, $"Bastión {DateTime.Now:yyyy-MM-dd HH.mm.ss}.png");
                File.WriteAllBytes(file, png);
                Debug.Log($"[captura] {file}");
                say?.Invoke(Saved);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[captura] no guardada: {e.Message}");
                say?.Invoke(Failed);
            }
            finally
            {
                taking = false;
            }
        }
    }
}
