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
    /// What the start screen's menu (0.12.24) asks of the save before a room is open: whether there is a campaign to go on
    /// with (SaveStore.Peek, «Continuar partida»), a new game over it (SaveStore.StartAfresh, «Nueva partida»), and the key
    /// typed apart («Unirse por dirección», GameAddress.IsKey).
    /// </summary>
    public class NetMenuTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "bastion-menu-" + Guid.NewGuid().ToString("N"));
        private readonly FakeCampaignRules campaign = new FakeCampaignRules();
        private readonly List<string> log = new List<string>();

        private string SavePath => Path.Combine(dir, "save.json");

        private string Beside(string label) => Path.Combine(dir, $"save.{label}.json");

        private SaveStore Store(string? path = null) => new SaveStore(path ?? SavePath, (line) => log.Add(line), campaign);

        private Room Host(SaveStore store) => new Room(store, new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(3), null, campaign);

        private static MemoryEnd Join(Room room, string name)
        {
            var (player, host) = MemoryEnd.Pair("jugador", "127.0.0.1:5000");
            room.Connect(host);
            room.Pump();
            Say(room, player, new { t = "hello", name, version = "1.0.0" });
            return player;
        }

        private static void Say(Room room, MemoryEnd player, object message)
        {
            player.Send(JObject.FromObject(message).ToString());
            room.Pump();
        }

        /// <summary>A save on disk, as the room writes it (without the store's backup and day copies).</summary>
        private void Write(SaveFile save, string? path = null)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path ?? SavePath, Json.Write(save));
        }

        private static SaveFile CampaignSave(int day, string? outcome = null, GameState? mission = null) => new SaveFile
        {
            Seats = new List<SavedSeat?> { new SavedSeat { Slot = 0, Name = "Ana", Token = "t0" }, null },
            Mode = "campaign",
            State = mission,
            Campaign = new CampaignState { Version = Campaign.CampaignVersion, Day = day, Level = 2, Doom = 3, MissionsFlown = day - 1, Outcome = outcome, Players = new List<int> { 0 } },
        };

        private string[] Files() => Directory.Exists(dir) ? Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy((f) => f).ToArray()! : new string[0];

        public void Dispose()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Fact]
        public void WithoutASaveThereIsNothingToGoOnWith()
        {
            Assert.Null(Store().Peek());
        }

        [Fact]
        public void TheCampaignIsToldAsTheLobbyTellsIt()
        {
            // Ana's campaign on the base, saved by the room; the menu reads it before any room is open.
            var room = Host(Store());
            var ana = Join(room, "Ana");
            Say(room, ana, new { t = "newCampaign" });
            var lobby = ana.Take("room").Last()["savedCampaign"]!;
            var peeked = Store().Peek();
            Assert.NotNull(peeked);
            Assert.Equal(lobby.ToString(), JToken.FromObject(peeked!, Json.Serializer).ToString());
            Assert.Equal(1, peeked!.Day);
            Assert.Null(peeked.InMission);
        }

        [Fact]
        public void AMissionLeftHalfPlayedIsNamed()
        {
            Write(CampaignSave(4, mission: new GameState { MapName = "Metsovo" }));
            var peeked = Store().Peek()!;
            Assert.Equal((4, 3, 3, "Metsovo"), (peeked.Day, peeked.Missions, peeked.Doom, peeked.InMission));
            // Over, it isn't half played any more.
            Write(CampaignSave(4, mission: new GameState { MapName = "Metsovo", Outcome = Outcomes.Victory }));
            Assert.Null(Store().Peek()!.InMission);
        }

        [Fact]
        public void NoCampaignOrOneOverIsNothingToGoOnWith()
        {
            Write(new SaveFile { Seats = new List<SavedSeat?> { new SavedSeat { Slot = 0, Name = "Ana", Token = "t0" }, null }, Mode = "skirmish", Phase = RoomPhases.Mission, State = new GameState { MapName = "Ciudad" } });
            Assert.Null(Store().Peek());
            Write(CampaignSave(9, outcome: "lost"));
            Assert.Null(Store().Peek());
            // Without the campaign's rules the room has none either.
            Write(CampaignSave(9));
            Assert.Null(new SaveStore(SavePath).Peek());
        }

        [Fact]
        public void LookingMovesNothingAndSaysNothing()
        {
            // A campaign from before the 0.7: the room keeps it aside on hosting; the look leaves it where it is.
            Directory.CreateDirectory(dir);
            File.WriteAllText(SavePath, "{\"version\":6,\"seats\":[],\"campaign\":{\"version\":3,\"day\":5}}");
            Assert.Null(Store().Peek());
            Assert.Equal(new[] { "save.json" }, Files());
            // One that can't be read: the backup tells, and the unreadable one stays as it is.
            File.WriteAllText(SavePath, "{ a medias");
            Write(CampaignSave(6), Beside("anterior"));
            Assert.Equal(6, Store().Peek()!.Day);
            Assert.Equal(new[] { "save.anterior.json", "save.json" }, Files());
            Assert.Equal("{ a medias", File.ReadAllText(SavePath));
            Assert.Empty(log);
        }

        [Fact]
        public void ANewGameKeepsTheOneThereWasAndStartsInTheLobby()
        {
            Write(CampaignSave(7));
            var before = File.ReadAllText(SavePath);
            var store = Store();
            store.StartAfresh();
            var room = Host(store);
            var ana = Join(room, "Ana");
            var first = ana.Take("room").Last();
            Assert.Equal(RoomPhases.Lobby, (string?)first["phase"]);
            Assert.Null(first["savedCampaign"]);
            Assert.Equal(before, File.ReadAllText(Beside(SaveStore.NewCampaignLabel)));
            Assert.Contains(log, (l) => l.Contains("Partida nueva"));
            // Her new campaign, alone: on the base, day 1; the one before, in its copy.
            Say(room, ana, new { t = "newCampaign" });
            Assert.Equal("base", (string?)ana.Take("room").Last()["phase"]);
            Assert.Equal(1, Store().Peek()!.Day);
            Assert.Equal(7, Store(Beside(SaveStore.NewCampaignLabel)).Peek()!.Day);
            // Another new game: another copy, the first one kept.
            var again = Store();
            again.StartAfresh();
            Host(again);
            Assert.Equal(1, Store(Beside($"{SaveStore.NewCampaignLabel}-2")).Peek()!.Day);
            Assert.Equal(7, Store(Beside(SaveStore.NewCampaignLabel)).Peek()!.Day);
        }

        [Fact]
        public void ANewGameWithNothingSavedKeepsNothing()
        {
            var store = Store();
            store.StartAfresh();
            var room = Host(store);
            Join(room, "Ana");
            Assert.DoesNotContain(Files(), (f) => f.Contains(SaveStore.NewCampaignLabel));
        }

        [Theory]
        [InlineData("KXRT5", true)]
        [InlineData(" kxr-t5 ", true)]
        [InlineData("KX RT 5", true)]
        [InlineData("KXRT", true)]
        [InlineData("KX", false)]
        [InlineData("KXRT5XY", false)]
        [InlineData("KX·RT5", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void AKeyTypedApartIsAKeysLength(string? text, bool key)
        {
            Assert.Equal(key, GameAddress.IsKey(text));
        }
    }
}
