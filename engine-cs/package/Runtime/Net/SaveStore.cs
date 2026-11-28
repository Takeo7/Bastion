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
    /// power cut, with the one before kept as a backup and a copy of the campaign as each of its last DayCopies stages
    /// began; one that cannot be read is moved aside, never written over.
    /// </summary>
    public sealed class SaveStore
    {
        /// <summary>persistence.ts SAVE_VERSION: the shape of the save.</summary>
        public const int SaveVersion = 6;

        /// <summary>
        /// The oldest campaign the host goes on with: the first of the runs (Campaign.RunsCampaignVersion, 16/11). Older ones
        /// are kept aside (OutdatedLabel) and the room starts anew; from it up to Campaign.RunsCampaignVersion they are
        /// brought up to date (ICampaignRules.MigrateCampaign); newer ones aren't read. It was Campaign.FreshCampaignVersion,
        /// the progression of the 0.7.
        /// </summary>
        public const int FreshVersion = 28;

        /// <summary>
        /// What a campaign older than FreshVersion is kept aside as, save.antes-de-las-runs.json: no older one goes on into
        /// the runs (Arturo, 16/11: set aside, and a new one). It was save.antes-0.7.json (docs/FASE9.md §7, FASE10.md).
        /// </summary>
        public const string OutdatedLabel = "antes-de-las-runs";

        /// <summary>
        /// Copies of the campaign kept, one as each stage of it began (a node chosen, CampaignState.NodesPlayed; one a day
        /// before the runs: Gameplay's review, 10/11, #11, with one backup only a save that wouldn't start left nothing to
        /// go back to): the newest so many.
        /// </summary>
        public const int DayCopies = 5;

        /// <summary>The copy of the save a new campaign leaves next to it (Room's «Nueva campaña», StartAfresh).</summary>
        public const string NewCampaignLabel = "antes-de-campana-nueva";

        private readonly string path;
        private readonly ICampaignRules? campaignRules;

        /// <summary>A line for the host's red.log; the room writes there too.</summary>
        public Action<string> Log { get; }

        /// <summary>Where a campaign older than FreshCampaignVersion was put aside on loading (OutdatedLabel), if one was.</summary>
        public string? SetAside { get; private set; }

        /// <summary>The file Load read the game from: the save, or its backup.</summary>
        public string? Loaded { get; private set; }

        /// <summary>The campaign stage of the save on disk (Load's, then each Save's): a Save on another one keeps a copy (KeepStage).</summary>
        private int? stageOnDisk;

        /// <summary>StartAfresh: the next Load keeps a copy and reads nothing.</summary>
        private bool afresh;

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
            if (afresh)
            {
                afresh = false;
                // The save, or the backup without one (Save stopped between its moves): kept as it is. If it can't be,
                // KeepAside throws and the host doesn't open, rather than start over it.
                var from = File.Exists(path) ? path : File.Exists(Backup) ? Backup : null;
                // One from before the runs under the name it would have had anyway: where the start screen said it is (OldCampaign).
                if (from != null) Log($"[save] Partida nueva: la de antes queda en {KeepAside(Outdated(from) ? OutdatedLabel : NewCampaignLabel, from, false)}.");
                stageOnDisk = null;
                return null;
            }
            var save = LoadOnce();
            stageOnDisk = StageOf(save?.Campaign);
            return save;
        }

        /// <summary>
        /// «Nueva partida» on the start screen (0.12.24), over whatever was saved: the next Load keeps a copy of the save as
        /// it is next to it, as the room's «Nueva campaña» does (&lt;name&gt;.antes-de-campana-nueva.json, -2, -3…), and reads
        /// nothing, so the room starts in the lobby with nothing, and its saves write over the one there was.
        /// </summary>
        public void StartAfresh()
        {
            afresh = true;
        }

        /// <summary>
        /// The campaign on disk as the room would go on with it, for the start screen's «Continuar partida» (0.12.24), read
        /// as Load reads it but moving nothing and saying nothing: the save, or its backup if it is missing or can't be
        /// read. Null without the campaign's rules, without a campaign, with one over, or with a save the room wouldn't
        /// take (a newer Bastión's, one older than FreshVersion, one that can't be read).
        /// </summary>
        public SavedCampaign? Peek()
        {
            if (campaignRules == null) return null;
            foreach (var file in new[] { path, Backup })
            {
                if (!File.Exists(file)) continue;
                if (!Read(file, out var save, out _, false)) continue;
                if (save == null) return null;
                var paused = save.Mode == "campaign" && save.State != null && save.State.Outcome == null ? save.State : null;
                return SavedCampaign.Of(save.Campaign, paused != null ? MissionName(paused) : null);
            }
            return null;
        }

        /// <summary>
        /// For the start screen, with no campaign to go on with (Peek null): the file name a campaign from before the runs
        /// is kept in (OutdatedLabel), or will be once the room opens, since the save or its backup is one; null without one.
        /// Arturo's and Ana's campaign (16/11): with the start screen's «Nueva partida» they seldom see the lobby, where the
        /// room says so (RoomMessage.SetAside). Like Peek, it moves nothing and says nothing.
        /// </summary>
        public string? PeekOutdated()
        {
            if (campaignRules == null || Peek() != null) return null;
            var aside = Beside(OutdatedLabel);
            if (File.Exists(aside) || Outdated(path) || Outdated(Backup)) return Path.GetFileName(aside);
            return null;
        }

        /// <summary>Whether `file` holds a campaign older than FreshVersion (one the room keeps aside); false if it isn't there or can't be read.</summary>
        private static bool Outdated(string file)
        {
            try
            {
                return File.Exists(file) && JToken.Parse(File.ReadAllText(file, Encoding.UTF8)) is JObject data && OutdatedCampaign(data);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                return false;
            }
        }

        /// <summary>A save's campaign older than FreshVersion (`campaign: null`, TypeScript's skirmishes, none).</summary>
        private static bool OutdatedCampaign(JObject data)
        {
            var version = (data["campaign"] as JObject)?["version"];
            return version?.Type == JTokenType.Integer && (int)version < FreshVersion;
        }

        /// <summary>
        /// The files an older Bastión leaves a campaign it can't read in, next to the save (27/11: the 0.12 line, opened again
        /// over an expedition): «Alojar partida» moves it aside as unreadable, «Nueva partida» copies it first.
        /// </summary>
        private static readonly string[] OlderAside = { "ilegible", NewCampaignLabel };

        /// <summary>What a save with nothing in it (a lobby's) is kept aside as when RecoverSetAside brings a campaign back over it.</summary>
        public const string LobbyLabel = "vestibulo";

        /// <summary>
        /// Brings back a campaign an older Bastión set aside (Producto, 27/11: Arturo and Ana opened the 0.12.26 and the
        /// 0.12.27 over their expedition, and the start screen had no «Continuar partida» after). Only with no campaign to go
        /// on with (Peek null), and over a save only an older one leaves behind:
        /// - a campaign from before the runs (its «Nueva partida», or one begun in its room): kept aside as the room keeps it
        ///   (OutdatedLabel), and the campaign looked for in both of OlderAside's files;
        /// - one with neither a campaign nor a mission (its «Alojar partida», in the lobby), or none: kept aside as LobbyLabel,
        ///   and the campaign looked for only among the unreadable ones, since this version never sets aside a campaign it can
        ///   read as that (its own «Nueva partida» copies are left alone).
        /// Of those holding a campaign to go on with, the newest on disk is moved back to the save's name. Where it came from,
        /// or null if nothing was done; throws if the save can't be kept aside, having moved nothing.
        /// </summary>
        public string? RecoverSetAside()
        {
            if (campaignRules == null || Peek() != null) return null;
            bool outdated = Outdated(path);
            if (File.Exists(path) && !outdated && !(Read(path, out var now, out _, false) && now != null && now.Campaign == null && now.State == null)) return null;
            var name = Path.GetFileNameWithoutExtension(path);
            var dir = Path.GetDirectoryName(path) ?? "";
            var labels = outdated ? OlderAside : new[] { "ilegible" };
            var from = Directory.Exists(dir)
                ? Directory.GetFiles(dir, $"{name}.*.json")
                    .Where((f) => labels.Any((l) => Regex.IsMatch(Path.GetFileName(f), $@"^{Regex.Escape(name)}\.{Regex.Escape(l)}(-\d+)?\.json$")))
                    .Where((f) => Read(f, out var save, out _, false) && save != null && SavedCampaign.Of(save.Campaign, null) != null)
                    .OrderByDescending((f) => File.GetLastWriteTimeUtc(f)).ThenByDescending((f) => f, StringComparer.Ordinal)
                    .FirstOrDefault()
                : null;
            if (from == null) return null;
            var aside = File.Exists(path) ? KeepAside(outdated ? OutdatedLabel : LobbyLabel, path, true) : null;
            try
            {
                File.Move(from, path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Nothing done: the save back where it was.
                if (aside != null) File.Move(aside, path);
                Log($"[save] No se pudo recuperar la campaña de {from} ({e.Message}); no se toca.");
                return null;
            }
            Log($"[save] Recuperada la campaña que una versión anterior apartó en {from}: vuelve a ser la partida guardada{(aside != null ? $"; la que había queda en {aside}" : "")}.");
            return from;
        }

        /// <summary>A mission left half played, as the lobby names it: the campaign's mission, or the map.</summary>
        public static string MissionName(GameState state)
        {
            return state.CampaignMission?.Name ?? state.MapName;
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

        /// <summary>
        /// The save in `file` (null: nothing to carry on with), or why it can't be read. `keep`: as the room loads it (an
        /// old campaign kept aside, the log told); without it, Peek's look, which moves and says nothing.
        /// </summary>
        private bool Read(string file, out SaveFile? save, out string? error, bool keep = true)
        {
            save = null;
            error = null;
            try
            {
                var data = JToken.Parse(File.ReadAllText(file, Encoding.UTF8)) as JObject;
                var version = data?["version"];
                if (data == null || version == null || version.Type != JTokenType.Integer)
                {
                    error = "no es una partida de Bastion";
                    return false;
                }
                // From a newer Bastión: taken down to this shape it would lose what it has, so it isn't read (Load keeps it aside).
                if ((int)version > SaveVersion)
                {
                    error = "es de una versión más nueva de Bastion";
                    return false;
                }
                // A campaign older than FreshVersion doesn't go on (the runs, 16/11; the progression of the 0.7 before): the
                // file is kept aside as it is and the room starts in the lobby. If it can't be kept aside, the host stops
                // (KeepAside throws, past the catch below), rather than write over it.
                if (OutdatedCampaign(data))
                {
                    if (!keep) return true;
                    SetAside = KeepAside(OutdatedLabel, file, false);
                    Log($"[save] Partida de antes de las runs: guardada aparte en {SetAside}; se empieza en el vestíbulo.");
                    return true;
                }
                // `campaign: null` in TypeScript's skirmishes: no campaign, as when it is missing.
                var campaignVersion = (data["campaign"] as JObject)?["version"];
                if (campaignVersion?.Type == JTokenType.Integer && (int)campaignVersion > Campaign.RunsCampaignVersion)
                {
                    error = "es de una versión más nueva de Bastion";
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
                    if (keep) Log($"[save] Partida guardada de una versión anterior ({(int)version}); se empieza de cero.");
                    return true;
                }
                save = data.ToObject<SaveFile>(Json.Serializer);
                if (save?.Campaign != null && campaignRules != null && save.Campaign.Version < Campaign.RunsCampaignVersion)
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
        /// the stages' copies. Each with its label for the players («anterior», «etapa-12»); not the file Load read. The days'
        /// copies of before the runs (dia-N) are left as they are: their campaign doesn't go on.
        /// </summary>
        public List<(string Label, string File)> Copies()
        {
            var copies = new List<(string, string)>();
            if (File.Exists(Backup) && Backup != Loaded) copies.Add(("anterior", Backup));
            foreach (var file in StageFiles()) copies.Add(($"{StagePrefix}{StageOf(file)}", file));
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
            var stage = StageOf(data.Campaign);
            if (stage != null && stage != stageOnDisk) KeepStage(stage.Value);
            stageOnDisk = stage;
        }

        /// <summary>The label of a stage's copy, before its number: etapa-3 (Lore's name for a node, 16/11).</summary>
        private const string StagePrefix = "etapa-";

        /// <summary>The stage a campaign is at, for its copies: the nodes chosen in it (NodesPlayed); null outside the runs.</summary>
        private static int? StageOf(CampaignState? c) => c != null && Campaign.IsRuns(c) ? c.NodesPlayed ?? 0 : (int?)null;

        /// <summary>
        /// The save as the campaign's stage `stage` began (the first written on it, as its node was chosen; a new campaign's
        /// stage 0 too) as &lt;name&gt;.etapa-&lt;stage&gt;.json, and only the newest DayCopies kept. One that can't be made is
        /// said, and the game goes on.
        /// </summary>
        private void KeepStage(int stage)
        {
            try
            {
                var copy = Beside($"{StagePrefix}{stage}");
                File.Copy(path, copy, true);
                // Newest by when it was made (a copy may keep its source's time).
                File.SetLastWriteTimeUtc(copy, DateTime.UtcNow);
                foreach (var old in StageFiles().Skip(DayCopies)) File.Delete(old);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log($"[save] No se pudo guardar la copia de la etapa {stage}: {e.Message}");
            }
        }

        /// <summary>The stages' copies on disk, newest first (by when they were made, then by stage).</summary>
        private List<string> StageFiles()
        {
            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir, $"{Path.GetFileNameWithoutExtension(path)}.{StagePrefix}*.json")
                .Where((f) => StageOf(f) != null)
                .OrderByDescending((f) => File.GetLastWriteTimeUtc(f)).ThenByDescending((f) => StageOf(f))
                .ToList();
        }

        private static int? StageOf(string file)
        {
            var m = Regex.Match(Path.GetFileName(file), $@"\.{StagePrefix}(\d{{1,6}})\.json$");
            return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null;
        }
    }
}
