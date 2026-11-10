using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bastion.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bastion.Net
{
    public sealed class SavedSeat
    {
        public int Slot { get; set; }
        public string Name { get; set; } = "";
        public string Token { get; set; } = "";
    }

    /// <summary>The room as saved (persistence.ts SaveFile), in the same JSON as TypeScript's: a 0.10.1 save is read as it is.</summary>
    public sealed class SaveFile
    {
        public int Version { get; set; } = SaveStore.SaveVersion;
        public List<SavedSeat?> Seats { get; set; } = new List<SavedSeat?>();
        public string Phase { get; set; } = RoomPhases.Lobby;
        public string Mode { get; set; } = "skirmish"; // skirmish | campaign
        public GameState? State { get; set; }
        public CampaignState? Campaign { get; set; }
        /// <summary>Whether the finished campaign mission has already been folded into the campaign.</summary>
        public bool ResultApplied { get; set; }
        public long[] Rng { get; set; } = new long[4];
        public string MissionKind { get; set; } = MissionKinds.Elimination;
        public string SkirmishMap { get; set; } = SkirmishMaps.City;
        public bool SkirmishVeterans { get; set; } = true;
        /// <summary>Skirmishes at night (docs/ALTURA-Y-NOCHE.md); missing in older saves: by day.</summary>
        public bool? SkirmishNight { get; set; }
    }

    /// <summary>
    /// The game on disk (persistence.ts SaveStore): written whole after every change, never cut short by a crash or a
    /// power cut, with the one before kept as a backup and a copy of the campaign as each of its last DayCopies days began;
    /// one that cannot be read is moved aside, never written over.
    /// </summary>
    public sealed class SaveStore
    {
        /// <summary>persistence.ts SAVE_VERSION: the shape of the save.</summary>
        public const int SaveVersion = 6;

        /// <summary>
        /// The release whose progression a campaign must be from (campaign.ts campaignOutdated, Campaign.FreshCampaignVersion):
        /// older ones are kept aside under its name, save.antes-&lt;release&gt;.json, and the messages say it.
        /// </summary>
        public const string FreshRelease = "0.7";

        /// <summary>
        /// Copies of the campaign kept, one a day of it (Gameplay's review, 10/11, #11: with one backup only, a save that
        /// wouldn't start left nothing to go back to): the newest so many.
        /// </summary>
        public const int DayCopies = 5;

        private readonly string path;
        private readonly ICampaignRules? campaignRules;

        /// <summary>A line for the host's red.log; the room writes there too.</summary>
        public Action<string> Log { get; }

        /// <summary>Where a campaign from before FreshRelease was put aside on loading, if one was (docs/FASE9.md §7, FASE10.md).</summary>
        public string? SetAside { get; private set; }

        /// <summary>The file Load read the game from: the save, or its backup.</summary>
        public string? Loaded { get; private set; }

        /// <summary>The campaign day of the save on disk (Load's, then each Save's): a Save on another one keeps a copy (KeepDay).</summary>
        private int? dayOnDisk;

        /// <summary>`campaignRules`: to bring a campaign of an older version up to date (without them, it is read as it is).</summary>
        public SaveStore(string path, Action<string>? log = null, ICampaignRules? campaignRules = null)
        {
            this.path = path;
            this.campaignRules = campaignRules;
            Log = log ?? ((_) => { });
        }

        /// <summary>The save before the last one, &lt;name&gt;.anterior.json: Save keeps it on every write, Load reads it when the save can't be.</summary>
        private string Backup => Beside("anterior");

        public SaveFile? Load()
        {
            var save = LoadOnce();
            dayOnDisk = save?.Campaign?.Day;
            return save;
        }

        private SaveFile? LoadOnce()
        {
            // No save but its backup: Save stopped between its two moves, and the backup is the last one.
            if (!File.Exists(path)) return File.Exists(Backup) ? FromBackup("no estaba") : null;
            Loaded = path;
            if (Read(path, out var save, out var error)) return save;
            // Never written over: moved aside as it is, and the game goes on from the backup if there is one. If it
            // can't be moved, KeepAside throws and the host stops rather than write over it.
            var aside = KeepAside("ilegible", path, true);
            Log($"[save] No se pudo leer {path} ({error}): queda en {aside}.");
            return File.Exists(Backup) ? FromBackup("no se podía leer") : null;
        }

        private SaveFile? FromBackup(string why)
        {
            Loaded = Backup;
            if (Read(Backup, out var save, out var error))
            {
                Log($"[save] La partida {why}: se sigue desde la copia anterior ({Backup}).");
                return save;
            }
            Log($"[save] Tampoco se pudo leer la copia anterior ({error}): se empieza en el vestíbulo.");
            return null;
        }

        /// <summary>The save in `file` (null: nothing to carry on with), or why it can't be read.</summary>
        private bool Read(string file, out SaveFile? save, out string? error)
        {
            save = null;
            error = null;
            try
            {
                var data = JToken.Parse(File.ReadAllText(file, Encoding.UTF8)) as JObject;
                var version = data?["version"];
                if (data == null || version == null || version.Type != JTokenType.Integer)
                {
                    error = "no es una partida de Bastión";
                    return false;
                }
                // From a newer Bastión: taken down to this shape it would lose what it has, so it isn't read (Load keeps it aside).
                if ((int)version > SaveVersion)
                {
                    error = "es de una versión más nueva de Bastión";
                    return false;
                }
                // A campaign from before FreshRelease can't take the new progression (docs/FASE9.md §7, FASE10.md): the file
                // is kept aside as it is and the room starts in the lobby. If it can't be kept aside, the host stops
                // (KeepAside throws, past the catch below), rather than write over it.
                // `campaign: null` in TypeScript's skirmishes: no campaign, as when it is missing.
                var campaignVersion = (data["campaign"] as JObject)?["version"];
                if (campaignVersion?.Type == JTokenType.Integer && (int)campaignVersion < Campaign.FreshCampaignVersion)
                {
                    SetAside = KeepAside($"antes-{FreshRelease}", file, false);
                    Log($"[save] Partida de antes de la {FreshRelease}: guardada aparte en {SetAside}; se empieza en el vestíbulo.");
                    return true;
                }
                if (campaignVersion?.Type == JTokenType.Integer && (int)campaignVersion > Campaign.CampaignVersion)
                {
                    error = "es de una versión más nueva de Bastión";
                    return false;
                }
                if (!(data["seats"] is JArray))
                {
                    error = "no tiene puestos";
                    return false;
                }
                // An older shape: TypeScript's, which the C# host never wrote.
                if ((int)version < SaveVersion)
                {
                    Log($"[save] Partida guardada de una versión anterior ({(int)version}); se empieza de cero.");
                    return true;
                }
                save = data.ToObject<SaveFile>(Json.Serializer);
                if (save?.Campaign != null && campaignRules != null && save.Campaign.Version != Campaign.CampaignVersion)
                {
                    // Whatever goes wrong bringing it up to date, the save is one that can't be read (persistence.ts migrate
                    // within its try): kept aside, never written over, and the host goes on.
                    try
                    {
                        save.Campaign = campaignRules.MigrateCampaign(save.Campaign);
                    }
                    catch (Exception e)
                    {
                        save = null;
                        error = $"no se pudo poner al día su campaña ({e.GetType().Name}: {e.Message})";
                        return false;
                    }
                }
                return true;
            }
            catch (Exception e) when (!(e is KeepAsideFailed) && (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is FormatException || e is InvalidCastException))
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// What to go back to when the save is read but the room won't start with it (Room), newest first: the backup, then
        /// the days' copies. Each with its label for the players («anterior», «dia-12»); not the file Load read.
        /// </summary>
        public List<(string Label, string File)> Copies()
        {
            var copies = new List<(string, string)>();
            if (File.Exists(Backup) && Backup != Loaded) copies.Add(("anterior", Backup));
            foreach (var file in DayFiles()) copies.Add(($"dia-{DayOf(file)}", file));
            return copies;
        }

        /// <summary>One of Copies, read as Load reads a save (brought up to date, the same checks); null if it can't be, said in red.log.</summary>
        public SaveFile? LoadCopy(string file)
        {
            if (Read(file, out var save, out var error)) return save;
            Log($"[save] No se pudo leer la copia {file} ({error}).");
            return null;
        }

        /// <summary>
        /// The save, read but one the room won't start with, moved aside as &lt;name&gt;.no-arranca.json (-2, -3…): never
        /// written over, and the next save doesn't take it for its backup. Where, or null without one; throws if it can't
        /// be moved (the host stops rather than lose it).
        /// </summary>
        public string? PutAside()
        {
            return File.Exists(path) ? KeepAside("no-arranca", path, true) : null;
        }

        /// <summary>A copy of the save as it is now, before something replaces it (a new campaign); null without a save. Throws if it can't.</summary>
        public string? KeepCopy(string label)
        {
            return File.Exists(path) ? KeepAside(label, path, false) : null;
        }

        /// <summary>
        /// Keeps `from`, as it is, next to the save as &lt;name&gt;.&lt;label&gt;.json (-2, -3… never over one already there);
        /// `move` takes it away instead of copying it. Returns where; throws if it can't.
        /// </summary>
        private string KeepAside(string label, string from, bool move)
        {
            for (int n = 1; ; n++)
            {
                var target = Beside(n > 1 ? $"{label}-{n}" : label);
                if (File.Exists(target)) continue;
                try
                {
                    if (move) File.Move(from, target);
                    else File.Copy(from, target, false);
                    return target;
                }
                catch (IOException) when (File.Exists(target))
                {
                    // Taken meanwhile: the next number.
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    throw new KeepAsideFailed($"No se pudo guardar aparte {from} ({e.Message}); no se toca.", e);
                }
            }
        }

        /// <summary>A save that couldn't be kept aside: never read past (the host stops rather than write over it).</summary>
        private sealed class KeepAsideFailed : IOException
        {
            public KeepAsideFailed(string message, Exception inner) : base(message, inner)
            {
            }
        }

        private string Beside(string label)
        {
            return Path.Combine(Path.GetDirectoryName(path) ?? "", $"{Path.GetFileNameWithoutExtension(path)}.{label}.json");
        }

        /// <summary>
        /// On the disk before it takes the save's name, so a crash or a power cut never leaves one cut short; the one it
        /// replaces stays as the backup.
        /// </summary>
        public void Save(SaveFile data)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            using (var file = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var text = new StreamWriter(file, new UTF8Encoding(false), 64 * 1024, true))
                {
                    Json.Serializer.Serialize(text, data);
                }
                file.Flush(true);
            }
            if (File.Exists(path))
            {
                if (File.Exists(Backup)) File.Delete(Backup);
                File.Move(path, Backup);
            }
            File.Move(tmp, path);
            var day = data.Campaign?.Day;
            if (day != null && day != dayOnDisk) KeepDay(day.Value);
            dayOnDisk = day;
        }

        /// <summary>
        /// The save as the campaign's day `day` began (the first written on it; a new campaign's first day too) as
        /// &lt;name&gt;.dia-&lt;day&gt;.json, and only the newest DayCopies kept. One that can't be made is said, and the game
        /// goes on.
        /// </summary>
        private void KeepDay(int day)
        {
            try
            {
                var copy = Beside($"dia-{day}");
                File.Copy(path, copy, true);
                // Newest by when it was made (a copy may keep its source's time).
                File.SetLastWriteTimeUtc(copy, DateTime.UtcNow);
                foreach (var old in DayFiles().Skip(DayCopies)) File.Delete(old);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log($"[save] No se pudo guardar la copia del día {day}: {e.Message}");
            }
        }

        /// <summary>The days' copies on disk, newest first (by when they were made, then by day).</summary>
        private List<string> DayFiles()
        {
            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir, $"{Path.GetFileNameWithoutExtension(path)}.dia-*.json")
                .Where((f) => DayOf(f) != null)
                .OrderByDescending((f) => File.GetLastWriteTimeUtc(f)).ThenByDescending((f) => DayOf(f))
                .ToList();
        }

        private static int? DayOf(string file)
        {
            var m = Regex.Match(Path.GetFileName(file), @"\.dia-(\d{1,6})\.json$");
            return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null;
        }
    }
}
