using System;
using System.Globalization;
using System.IO;

namespace Bastion.Net
{
    /// <summary>
    /// red.log, next to the save (index.ts netLog): who connects and how, what each app reports (how it draws, its errors
    /// and hangs) and what the room has to say, to find out why a partner cannot play. Lines from any thread.
    /// </summary>
    public sealed class NetLog
    {
        /// <summary>Past this, red.log starts again and the old one stays as red.anterior.log: a few days of play each.</summary>
        public const long MaxBytes = 8 * 1024 * 1024;

        private readonly string path;
        private readonly object gate = new object();
        private int lines;

        public NetLog(string path)
        {
            this.path = path;
        }

        public void Write(string line)
        {
            lock (gate)
            {
                try
                {
                    // Two players send a line every 10 s: checked now and then, not on every line.
                    if (lines++ % 500 == 0 && File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                    {
                        var old = Path.ChangeExtension(path, ".anterior.log");
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(path, $"{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)} {line}\n");
                }
                catch (Exception)
                {
                    /* the log helps, it never stops the game */
                }
            }
        }
    }
}
