using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign an older Bastión set aside, back as the save (SaveStore.RecoverSetAside; Producto, 27/11: Arturo and
    /// Ana opened the 0.12.26 and the 0.12.27 over their expedition). Its «Alojar partida» moves the save aside as
    /// unreadable and writes a lobby's; its «Nueva partida» copies it as save.antes-de-campana-nueva.json and writes a
    /// campaign from before the runs.
    /// </summary>
    public class NetSaveRecoverTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "bastion-recover-" + Guid.NewGuid().ToString("N"));
        private readonly FakeCampaignRules campaign = new FakeCampaignRules();
        private readonly List<string> log = new List<string>();

        private string SavePath => Path.Combine(dir, "save.json");

        private string Beside(string label) => Path.Combine(dir, $"save.{label}.json");

        private SaveStore Store() => new SaveStore(SavePath, (line) => log.Add(line), campaign);

        /// <summary>A save on disk, written `minutesAgo` before now.</summary>
        private string Write(string text, string? path = null, int minutesAgo = 0)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path ?? SavePath, text);
            File.SetLastWriteTimeUtc(path ?? SavePath, DateTime.UtcNow.AddMinutes(-minutesAgo));
            return text;
        }

        private static string Expedition(int day, string? outcome = null) => Json.Write(new SaveFile
        {
            Seats = new List<SavedSeat?> { new SavedSeat { Slot = 0, Name = "Ana", Token = "t0" }, null },
            Mode = "campaign",
            Campaign = new CampaignState { Version = Campaign.RunsCampaignVersion, Day = day, Level = 2, Doom = 3, MissionsFlown = day - 1, Outcome = outcome, Players = new List<int> { 0 } },
        });

        /// <summary>The 0.12's campaign (version 27), as its «Nueva partida» leaves it.</summary>
        private static string Older() => $"{{\"version\":6,\"seats\":[],\"phase\":\"base\",\"mode\":\"campaign\",\"campaign\":{{\"version\":{SaveStore.FreshVersion - 1},\"day\":2}}}}";

        /// <summary>The 0.12's lobby, as its «Alojar partida» leaves it once the host is in.</summary>
        private static string Lobby() => Json.Write(new SaveFile { Seats = new List<SavedSeat?> { new SavedSeat { Slot = 0, Name = "Arturo", Token = "t1" }, null } });

        private string[] Files() => Directory.Exists(dir) ? Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy((f) => f).ToArray()! : new string[0];

        public void Dispose()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Fact]
        public void AfterTheOlderNewGameTheExpeditionComesBack()
        {
            // An older «Nueva partida» of this version's own, and the 0.12's, newer, over the expedition of day 9.
            var before = Write(Expedition(5), Beside("antes-de-campana-nueva"), 120);
            var kept = Write(Expedition(9), Beside("antes-de-campana-nueva-2"), 30);
            var older = Write(Older());
            Assert.Null(Store().Peek());
            Assert.Equal(Beside("antes-de-campana-nueva-2"), Store().RecoverSetAside());
            Assert.Equal(kept, File.ReadAllText(SavePath));
            Assert.Equal(9, Store().Peek()!.Day);
            // The 0.12's, where this version keeps one from before the runs; the older copy as it was.
            Assert.Equal(older, File.ReadAllText(Beside(SaveStore.OutdatedLabel)));
            Assert.Equal(before, File.ReadAllText(Beside("antes-de-campana-nueva")));
            Assert.Equal(new[] { "save.antes-de-campana-nueva.json", $"save.{SaveStore.OutdatedLabel}.json", "save.json" }, Files());
            Assert.Contains(log, (l) => l.StartsWith("[save] Recuperada la campaña") && l.Contains("antes-de-campana-nueva-2"));
        }

        [Fact]
        public void AfterTheOlderHostTheExpeditionComesBack()
        {
            var moved = Write(Expedition(7), Beside("ilegible"), 60);
            // This version's own «Nueva partida», newer: set aside on purpose, it stays so.
            var own = Write(Expedition(3), Beside("antes-de-campana-nueva"), 10);
            var lobby = Write(Lobby());
            Assert.Equal(Beside("ilegible"), Store().RecoverSetAside());
            Assert.Equal(moved, File.ReadAllText(SavePath));
            Assert.Equal(lobby, File.ReadAllText(Beside(SaveStore.LobbyLabel)));
            Assert.Equal(own, File.ReadAllText(Beside("antes-de-campana-nueva")));
            Assert.Equal(7, Store().Peek()!.Day);
            // Once back, nothing more to do.
            Assert.Null(Store().RecoverSetAside());
        }

        [Fact]
        public void WithNoSaveTheExpeditionComesBackToo()
        {
            var moved = Write(Expedition(4), Beside("ilegible-2"), 5);
            Assert.Equal(Beside("ilegible-2"), Store().RecoverSetAside());
            Assert.Equal(moved, File.ReadAllText(SavePath));
            Assert.Equal(new[] { "save.json" }, Files());
        }

        [Fact]
        public void NeverOverAGameOfThisVersion()
        {
            Write(Expedition(7), Beside("ilegible"), 60);
            Write(Expedition(8), Beside("antes-de-campana-nueva"), 60);
            // A campaign to go on with, one over, and a mission under way without one: each stays the save.
            foreach (var save in new[]
            {
                Expedition(2),
                Expedition(12, "lost"),
                Json.Write(new SaveFile { Seats = new List<SavedSeat?> { null, null }, Mode = "skirmish", Phase = RoomPhases.Mission, State = new GameState { MapName = "Ciudad" } }),
            })
            {
                Write(save);
                Assert.Null(Store().RecoverSetAside());
                Assert.Equal(save, File.ReadAllText(SavePath));
                Assert.Equal(new[] { "save.antes-de-campana-nueva.json", "save.ilegible.json", "save.json" }, Files());
            }
            // Its backup to go on with (the save missing): the room goes on from it, as before.
            File.Delete(SavePath);
            Write(Expedition(6), Beside("anterior"));
            Assert.Null(Store().RecoverSetAside());
            Assert.Empty(log);
        }

        [Fact]
        public void NothingToBringBackMovesNothing()
        {
            var older = Write(Older());
            // Unreadable, of a newer Bastión, from before the runs, over, or under another name: none is one to go on with.
            Write("{ a medias", Beside("ilegible"));
            Write("{\"version\":7,\"seats\":[]}", Beside("ilegible-2"));
            Write(Older(), Beside("antes-de-campana-nueva"));
            Write(Expedition(9, "won"), Beside("antes-de-campana-nueva-2"));
            Write(Expedition(9), Beside("etapa-4"));
            Write(Expedition(9), Beside("no-arranca"));
            var files = Files();
            Assert.Null(Store().RecoverSetAside());
            Assert.Equal(files, Files());
            Assert.Equal(older, File.ReadAllText(SavePath));
            // Without the campaign's rules, nothing either (as the room).
            Write(Expedition(9), Beside("ilegible-3"));
            Assert.Null(new SaveStore(SavePath).RecoverSetAside());
            Assert.Empty(log);
        }

        [Fact]
        public void TheOneFromBeforeTheRunsIsNeverWrittenOver()
        {
            // One already kept from 16/11, when the runs came: the 0.12's goes next to it.
            var first = Write(Older().Replace("\"day\":2", "\"day\":40"), Beside(SaveStore.OutdatedLabel));
            var older = Write(Older());
            Write(Expedition(9), Beside("antes-de-campana-nueva"), 5);
            Assert.NotNull(Store().RecoverSetAside());
            Assert.Equal(first, File.ReadAllText(Beside(SaveStore.OutdatedLabel)));
            Assert.Equal(older, File.ReadAllText(Beside($"{SaveStore.OutdatedLabel}-2")));
        }
    }
}
