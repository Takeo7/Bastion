using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The save's copies (Net/SaveStore.cs) and a room whose save won't start (Net/Room.cs), Gameplay's review, 10/11, #11:
    /// with one backup only, a save that made the room throw left the host unable to open a game, every time.
    /// </summary>
    public sealed class NetSaveCopiesTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "bastion-copias-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> log = new List<string>();

        private string SavePath => Path.Combine(dir, "save.json");

        private string Beside(string label) => Path.Combine(dir, $"save.{label}.json");

        public void Dispose()
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception)
            {
                /* a temporary folder */
            }
        }

        /// <summary>A campaign of runs as its stage `stage` goes (CampaignState.NodesPlayed).</summary>
        private static SaveFile AtStage(int stage) => new SaveFile
        {
            Phase = RoomPhases.Base,
            Mode = "campaign",
            Campaign = new CampaignState { Version = Campaign.RunsCampaignVersion, Stash = new GearGrid { W = Runs.StashW, H = Runs.StashH }, NodesPlayed = stage },
            Seats = { new SavedSeat { Slot = 0, Name = "Ana", Token = "t-ana" }, null },
        };

        /// <summary>A skirmish's save with Ana in the first seat by `token`; `slot` other than 0, one the room won't start with.</summary>
        private void Write(string file, string token, int slot = 0)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(file, Json.Write(new SaveFile { Seats = { new SavedSeat { Slot = slot, Name = "Ana", Token = token }, null } }));
        }

        private Room Make() => new Room(new SaveStore(SavePath, log.Add), new FakeRules(), "1.0.0", clock: new FakeClock(), rng: new Rng(7));

        /// <summary>Ana's app, back with `token`: what it heard.</summary>
        private static MemoryEnd Join(Room room, string token)
        {
            var (player, host) = MemoryEnd.Pair("jugador", "10.0.0.1:5000");
            room.Connect(host);
            room.Pump();
            player.Send(JObject.FromObject(new { t = "hello", name = "Ana", token, version = "1.0.0" }).ToString());
            room.Pump();
            return player;
        }

        [Fact]
        public void EachStageOfTheCampaignKeepsACopyAndTheLastFiveStay()
        {
            var store = new SaveStore(SavePath, log.Add);
            // Several saves a stage: one copy, as the stage began (its node chosen).
            for (int stage = 1; stage <= 7; stage++)
                for (int i = 0; i < 3; i++)
                    store.Save(AtStage(stage));
            Assert.Equal(new[] { "save.etapa-3.json", "save.etapa-4.json", "save.etapa-5.json", "save.etapa-6.json", "save.etapa-7.json" }, Directory.GetFiles(dir, "save.etapa-*.json").Select(Path.GetFileName).OrderBy((n) => n));
            Assert.Equal(7, Json.Read<SaveFile>(File.ReadAllText(Beside("etapa-7"))).Campaign!.NodesPlayed);
            // Opened again on the same stage: no copy. A new campaign's stage 0: one, and the oldest goes.
            store = new SaveStore(SavePath, log.Add);
            store.Load();
            var seventh = File.GetLastWriteTimeUtc(Beside("etapa-7"));
            store.Save(AtStage(7));
            Assert.Equal(seventh, File.GetLastWriteTimeUtc(Beside("etapa-7")));
            store.Save(AtStage(0));
            Assert.True(File.Exists(Beside("etapa-0")));
            Assert.False(File.Exists(Beside("etapa-3")));
            // What a room that won't start goes back to, newest first.
            var copies = store.Copies().Select((c) => c.Label).ToList();
            Assert.Equal(new[] { "anterior", "etapa-0" }, copies.Take(2));
            Assert.Equal(new[] { "etapa-4", "etapa-5", "etapa-6", "etapa-7" }, copies.Skip(2).OrderBy((l) => l));
            Assert.Empty(log);
        }

        [Fact]
        public void TheDaysCopiesOfBeforeTheRunsAreLeftAsTheyAre()
        {
            // Arturo's and Ana's (save.dia-N.json): their campaign doesn't go on, so they are neither offered nor pruned.
            Write(Beside("dia-40"), "t-ana");
            var store = new SaveStore(SavePath, log.Add);
            for (int stage = 0; stage <= 6; stage++) store.Save(AtStage(stage));
            Assert.True(File.Exists(Beside("dia-40")));
            Assert.DoesNotContain(store.Copies(), (c) => c.Label.StartsWith("dia-"));
            // A campaign that isn't one of runs (none the host makes now) keeps none.
            Directory.Delete(dir, true);
            store = new SaveStore(SavePath, log.Add);
            var before = AtStage(3);
            before.Campaign!.Stash = null;
            store.Save(before);
            Assert.Empty(Directory.GetFiles(dir, "save.etapa-*.json"));
        }

        [Fact]
        public void ASaveThatWontStartGoesAsideAndTheBackupGoesOn()
        {
            // Read, but with Ana in the second seat's place: Summarize threw, and with it every try to host.
            Write(SavePath, "t-rota", slot: 7);
            Write(Beside("anterior"), "t-ana");
            var room = Make();
            Assert.True(File.Exists(Beside("no-arranca")));
            Assert.Contains(log, (l) => l.StartsWith("[save] La partida guardada no arranca (InvalidDataException: el puesto 1 dice ser el 8)"));
            Assert.Contains(log, (l) => l.StartsWith("[save] Se sigue desde ") && l.EndsWith("save.anterior.json."));
            // Ana's seat, by her token; the lobby says which copy.
            var ana = Join(room, "t-ana");
            var welcome = ana.Take("welcome").Single();
            Assert.Equal(0, (int)welcome["slot"]!);
            Assert.Equal("t-ana", (string?)welcome["token"]);
            const string told = "La partida guardada no se ha podido abrir. Se sigue desde la copia anterior. La que falló queda aparte, en save.no-arranca.json.";
            Assert.Equal(told, (string?)ana.Take("room").Last()["restored"]);
            // Told once, wherever she lands; not again when she comes back.
            Assert.Equal(told, (string?)ana.Take("error").Single()["message"]);
            ana.Close("vuelve");
            room.Pump();
            Assert.Empty(Join(room, "t-ana").Take("error"));
            // Written as it goes on: the next start needs no copy.
            log.Clear();
            Make();
            Assert.DoesNotContain(log, (l) => l.Contains("no arranca"));
        }

        [Fact]
        public void WithoutABackupThatStartsTheNewestStageGoesOn()
        {
            Write(SavePath, "t-rota", slot: 7);
            Write(Beside("anterior"), "t-rota", slot: 5);
            Write(Beside("etapa-2"), "t-dos");
            Write(Beside("etapa-3"), "t-ana");
            File.SetLastWriteTimeUtc(Beside("etapa-2"), DateTime.UtcNow.AddDays(-2));
            File.SetLastWriteTimeUtc(Beside("etapa-3"), DateTime.UtcNow.AddDays(-1));
            var room = Make();
            Assert.Contains(log, (l) => l.StartsWith("[save] Tampoco arranca ") && l.Contains("save.anterior.json"));
            var ana = Join(room, "t-ana");
            Assert.Equal("t-ana", (string?)ana.Take("welcome").Single()["token"]);
            Assert.StartsWith("La partida guardada no se ha podido abrir. Se sigue desde la copia de cuando llevabais 3 etapas jugadas.", (string?)ana.Take("room").Last()["restored"]);
        }

        [Fact]
        public void WhenNothingStartsTheLobbyIsNewAndSaysSo()
        {
            Write(SavePath, "t-rota", slot: 7);
            var room = Make();
            Assert.Contains("[save] Ninguna copia arranca: se empieza en el vestíbulo.", log);
            Assert.Equal(RoomPhases.Lobby, room.Summary().phase);
            var ana = Join(room, "t-rota");
            // A new seat: the token of the save kept aside isn't one any more.
            Assert.NotEqual("t-rota", (string?)ana.Take("welcome").Single()["token"]);
            Assert.Equal("La partida guardada no se ha podido abrir, ni ninguna de sus copias. Hay que empezar una nueva. La que falló queda aparte, en save.no-arranca.json.", (string?)ana.Take("room").Last()["restored"]);
        }

        [Fact]
        public void AGoodSaveSaysNothing()
        {
            Write(SavePath, "t-ana");
            var room = Make();
            var ana = Join(room, "t-ana");
            Assert.Null(ana.Take("room").Last()["restored"]);
            Assert.Empty(ana.Take("error"));
            Assert.False(File.Exists(Beside("no-arranca")));
        }
    }
}
