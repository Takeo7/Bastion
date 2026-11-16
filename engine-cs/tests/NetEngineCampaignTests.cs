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
    /// The engine's campaign as the room asks for it (Net/EngineCampaignRules.cs) against TypeScript's: missions of
    /// the campaign launched as room.ts launchCampaignMission does, with the same seed, must come out the same, to the
    /// last field and the Rng after (golden/net/launch.json, record-launch.ts).
    /// </summary>
    public class NetEngineCampaignTests
    {
        private static readonly string Golden = Path.Combine(AppContext.BaseDirectory, "golden");

        private static readonly JArray Launched = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Golden, "net", "launch.json")))["missions"]!;

        public static IEnumerable<object[]> Sources() => Launched.Select((m) => new object[] { (string)m["source"]! });

        /// <summary>A campaign of golden/campaign/ by record-launch.ts's name for it: states.json#name or saves/file.</summary>
        private static JToken CampaignOf(string source)
        {
            if (source.StartsWith("states.json#"))
            {
                var name = source.Substring("states.json#".Length);
                var states = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Golden, "campaign", "states.json")))["states"]!;
                return states.Single((s) => (string)s["name"]! == name)["campaign"]!;
            }
            return JObject.Parse(File.ReadAllText(Path.Combine(Golden, "campaign", source)))["campaign"]!;
        }

        [Theory]
        [MemberData(nameof(Sources))]
        public void AMissionIsLaunchedAsTheTypeScriptHostDoes(string source)
        {
            var recorded = Launched.Single((m) => (string)m["source"]! == source);
            var c = CampaignOf(source).ToObject<CampaignState>(Json.Serializer)!;
            var before = Json.Write(c);
            var rng = new Rng((int)recorded["seed"]!);
            var made = new EngineCampaignRules().LaunchMission(c, rng);
            Assert.NotNull(made);
            Assert.Equal(Campaign.ActiveOffer(c)!.Name, made!.Value.name);
            var difference = Json2.FirstDifference(Json2.Clean(recorded["state"]!), Json2.Clean(JObject.Parse(Json.Write(made.Value.state))));
            Assert.True(difference == null, $"{source}: {difference}");
            Assert.Equal(recorded["rng"]!.Select((x) => (long)x), rng.State);
            // It only reads the campaign: the room broadcasts it as it was.
            Assert.Equal(before, Json.Write(c));
        }

        [Fact]
        public void WithoutAnOperationChosenNothingIsLaunched()
        {
            var c = CampaignOf("states.json#mission-2").ToObject<CampaignState>(Json.Serializer)!;
            Assert.Null(c.ActiveMission);
            Assert.Null(new EngineCampaignRules().LaunchMission(c, new Rng(1)));
        }

        /// <summary>A room with the engine's rules and campaign, on a save of its own: what the game's host runs.</summary>
        private sealed class Host : IDisposable
        {
            public readonly string Dir = Path.Combine(Path.GetTempPath(), "bastion-campana-motor-" + Guid.NewGuid().ToString("N"));
            public readonly List<string> Log = new List<string>();
            public readonly FakeClock Clock = new FakeClock();
            public Room Room;
            private int peers;

            public Host()
            {
                Room = Make();
            }

            public string SavePath => Path.Combine(Dir, "save.json");

            public Room Make()
            {
                var rules = new EngineCampaignRules();
                return new Room(new SaveStore(SavePath, Log.Add, rules), new EngineRules(), "1.0.0", null, Clock, new Rng(11), null, rules);
            }

            /// <summary>The campaign as the room last saved it.</summary>
            public CampaignState Saved()
            {
                return new SaveStore(SavePath, null, new EngineCampaignRules()).Load()!.Campaign!;
            }

            public void Advance(long ms)
            {
                Clock.Advance(ms);
                Room.Pump();
            }

            public MemoryEnd Join(string name, string? token = null)
            {
                var (player, host) = MemoryEnd.Pair("jugador", $"10.0.0.{++peers}:5000");
                Room.Connect(host);
                Room.Pump();
                Say(player, new { t = "hello", name, token, version = "1.0.0" });
                return player;
            }

            public void Say(MemoryEnd player, object message)
            {
                player.Send(JObject.FromObject(message).ToString());
                Room.Pump();
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Dir, true);
                }
                catch (Exception)
                {
                    /* a temporary folder */
                }
            }
        }

        private static JToken CampaignSent(MemoryEnd player) => player.Take("campaign").Last()["state"]!;


        private static object Ccmd(object cmd) => new { t = "ccmd", cmd };

        /// <summary>The ids of the run's nodes in `row`, as both heard them.</summary>
        private static List<string> RowOf(JToken campaign, int row) => campaign["run"]!["nodes"]!.Where((n) => (int)n["row"]! == row).Select((n) => (string)n["id"]!).ToList();

        /// <summary>Ana and Pepe out on a run, by Ana's proposals and Pepe's yes, and into the fight of the first row's first node.</summary>
        private static void GoOut(Host host, MemoryEnd ana, MemoryEnd pepe)
        {
            host.Say(ana, Ccmd(new { type = "startRun" }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            var node = RowOf(CampaignSent(ana), 1).First();
            host.Say(ana, Ccmd(new { type = "chooseNode", node }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            Assert.Equal("mission", host.Room.Summary().phase);
        }

        [Fact]
        public void TwoCommandersGoOutOnARunAndChooseTheirWay()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            host.Say(ana, new { t = "newCampaign" });
            // A campaign of runs (16/11): two soldiers each, with their knives, all going out.
            var c = CampaignSent(pepe);
            Assert.Equal(Campaign.RunsCampaignVersion, (int)c["version"]!);
            Assert.Equal(new[] { 0, 1 }, c["players"]!.Select((x) => (int)x));
            Assert.Equal(4, c["squad"]!.Count());
            Assert.All(c["soldiers"]!, (s) => Assert.Equal(FoundWeaponIds.Knife, (string?)s["kit"]!["melee"]!["type"]));
            // Out, by Ana's proposal and Pepe's yes.
            host.Say(ana, Ccmd(new { type = "startRun" }));
            Assert.Equal("startRun", (string?)CampaignSent(pepe)["proposal"]!["action"]!["type"]);
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            c = CampaignSent(ana);
            Assert.Equal(RunPhases.Choose, (string?)c["run"]!["phase"]);
            // The way, by Pepe's proposal and Ana's yes: a node of the first row, a fight.
            var node = RowOf(c, 1).First();
            host.Say(pepe, Ccmd(new { type = "chooseNode", node }));
            host.Say(ana, Ccmd(new { type = "vote", accept = true }));
            var after = host.Saved();
            Assert.Equal((node, RunPhases.Fight, 1), (after.Run!.At, after.Run.Phase, after.NodesPlayed ?? 0));
            Assert.Null(after.Proposal);
            Assert.NotEmpty(pepe.Take("campaign"));
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo"));
        }

        [Fact]
        public void ARunEndedOnItsMapGoesBackToTheBase()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            host.Say(ana, new { t = "newCampaign" });
            // Alone, at once.
            host.Say(ana, Ccmd(new { type = "startRun" }));
            Assert.NotNull(host.Saved().Run);
            host.Say(ana, Ccmd(new { type = "endRun" }));
            Assert.Null(host.Saved().Run);
            Assert.Null(CampaignSent(ana)["run"]);
            Assert.Equal("base", host.Room.Summary().phase);
            // The summary says it is on the base.
            var saved = ana.Take("room").Last()["savedCampaign"]!;
            Assert.Null(saved["row"]);
        }

        [Fact]
        public void OnTheBaseTheSquadIsForWhoIsIn()
        {
            // Gameplay, 16/11: with no run under way, the seats are shared out again whenever one comes or goes, so nobody
            // has to take soldiers out for the other's (StartRunRefusal, the most each player takes).
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            host.Say(ana, new { t = "newCampaign" });
            var token = (string)pepe.Take("welcome").Single()["token"]!;
            int Theirs(CampaignState c, int slot) => c.Squad.Count((id) => c.Soldiers.Single((s) => s.Id == id).Owner == slot);
            Assert.Equal((2, 2), (Theirs(host.Saved(), 0), Theirs(host.Saved(), 1)));
            pepe.Close("se va");
            host.Room.Pump();
            Assert.Equal((2, 0), (Theirs(host.Saved(), 0), Theirs(host.Saved(), 1)));
            Assert.Equal(2, CampaignSent(ana)["squad"]!.Count());
            pepe = host.Join("Pepe", token);
            Assert.Equal((2, 2), (Theirs(host.Saved(), 0), Theirs(host.Saved(), 1)));
            // Out, the squad stays as it went.
            host.Say(ana, Ccmd(new { type = "startRun" }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            Assert.NotNull(host.Saved().Run);
        }

        [Fact]
        public void APartnerJoiningARunUnderWayIsHandedHalfTheSquad()
        {
            // Runs.HandOver (16/11): Ana went out alone with her four; Pepe, coming, takes two of them till the run ends, and
            // gets two of his own on the base.
            using var host = new Host();
            var ana = host.Join("Ana");
            host.Say(ana, new { t = "newCampaign" });
            host.Say(ana, Ccmd(new { type = "startRun" }));
            var squad = host.Saved().Run!.Squad;
            Assert.Equal(4, squad.Count);
            var pepe = host.Join("Pepe");
            var c = CampaignSent(pepe);
            var hands = ((JObject)c["run"]!["hands"]!).Properties().ToDictionary((p) => p.Name, (p) => (int)p.Value);
            Assert.Equal(2, hands.Count);
            Assert.All(hands, (h) => Assert.Equal(1, h.Value));
            Assert.All(hands.Keys, (id) => Assert.Contains(id, squad));
            Assert.Equal(2, c["soldiers"]!.Count((s) => (int)s["owner"]! == 1));
            Assert.NotEmpty(ana.Take("campaign"));
        }

        [Fact]
        public void APartnerArrivingLateGetsSoldiersOfTheirOwnInTheSquad()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            host.Say(ana, new { t = "newCampaign" });
            Assert.Equal(new[] { 0 }, CampaignSent(ana)["players"]!.Select((x) => (int)x));
            var pepe = host.Join("Pepe");
            var c = CampaignSent(pepe);
            Assert.Equal(new[] { 0, 1 }, c["players"]!.Select((x) => (int)x));
            var theirs = c["soldiers"]!.Where((s) => (int)s["owner"]! == 1).Select((s) => (string)s["id"]!).ToList();
            Assert.NotEmpty(theirs);
            Assert.Contains(c["squad"]!.Select((x) => (string)x!), (id) => theirs.Contains(id));
        }

        /// <summary>
        /// A vote of the runs (going out) goes ahead with nobody answering, as the base's proposals did (Ana's notes of 2/11,
        /// #4: «Un desertor», that the players saw reach nought and not go ahead). Whoever proposes it, host or guest, the
        /// room says yes for the other after 30 s; with their window out of sight, after 5 min; dropped, as when here or away.
        /// </summary>
        [Theory]
        [InlineData(0, "here", 30_000L)]
        [InlineData(1, "here", 30_000L)]
        [InlineData(0, "away", Room.AwayGraceMs)]
        [InlineData(1, "away", Room.AwayGraceMs)]
        [InlineData(0, "gone", 30_000L)]
        [InlineData(1, "away and gone", Room.AwayGraceMs)]
        public void AVoteNobodyAnswersGoesAhead(int by, string other, long wait)
        {
            using var host = new Host();
            var players = new[] { host.Join("Ana"), host.Join("Pepe") };
            host.Say(players[0], new { t = "newCampaign" });
            var (proposer, answerer) = (players[by], players[1 - by]);
            host.Say(proposer, Ccmd(new { type = "startRun" }));
            Assert.Equal("startRun", (string?)CampaignSent(answerer)["proposal"]!["action"]!["type"]);
            if (other.StartsWith("away")) host.Say(answerer, new { t = "away", away = true });
            if (other.EndsWith("gone"))
            {
                answerer.Close("dormido");
                host.Room.Pump();
            }
            if (wait > 30_000)
            {
                // Away: at 30 s the wait goes on, and both see what is left of the five minutes («se acepta sola en 4:30»).
                host.Advance(30_000);
                Assert.Equal(wait - 30_000, (long)proposer.Take("deadline").Last()["ms"]!);
                host.Advance(wait - 30_000 - 1);
            }
            else host.Advance(wait - 1);
            Assert.Null(host.Saved().Run);
            host.Advance(1);
            // Out: and both hear it is over.
            var c = host.Saved();
            Assert.NotNull(c.Run);
            Assert.Null(c.Proposal);
            var told = CampaignSent(proposer);
            Assert.Null(told["proposal"]);
            Assert.NotNull(told["run"]);
            Assert.Null(proposer.Take("deadline").Last()["ms"]);
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo"));
            // red.log says so, and, away, that it waited for them.
            var name = by == 0 ? "Pepe" : "Ana";
            Assert.Contains($"[campaña] Propuesta sin respuesta de {name}: {RunTexts.StartRunText(1)}, sigue adelante", host.Log);
            Assert.Equal(wait > 30_000, host.Log.Contains($"[campaña] {name} no tiene el juego a la vista: la propuesta espera 270 s más"));
        }

        [Fact]
        public void AnOrderTheCampaignRefusesSaysWhyAndIsLogged()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            host.Say(ana, new { t = "newCampaign" });
            var first = CampaignSent(ana)["soldiers"]![0]!;
            var soldier = (string)first["id"]!;
            // A new campaign has no supplies for the stash's first upgrade.
            var price = Content.UpgradePrices[UpgradeIds.Stash][0];
            host.Say(ana, Ccmd(new { type = "upgrade", upgrade = UpgradeIds.Stash }));
            Assert.Equal(RunTexts.Missing(price), (string?)ana.Take("rejected").Single()["message"]);
            Assert.Empty(ana.Take("campaign"));
            Assert.Contains($"[campaña] Ana (puesto 1): «upgrade» rechazada: {RunTexts.Missing(price)}", host.Log);
            // A medicine that isn't there, for her own soldier: red.log says whose soldier it was, and how it stood.
            host.Say(ana, Ccmd(new { type = "useGear", gear = "g999", soldier }));
            Assert.NotEmpty((string?)ana.Take("rejected").Single()["message"]!);
            Assert.Contains(host.Log, (l) => l.StartsWith("[campaña] Ana (puesto 1): «useGear» rechazada: ") && l.Contains($" · {(string)first["name"]!} ({soldier}), de 1, vivo,"));
        }

        [Fact]
        public void ACampaignOfBeforeTheRunsIsKeptAsideWhenTheHostOpens()
        {
            using var host = new Host();
            Directory.CreateDirectory(host.Dir);
            var save = new JObject
            {
                ["version"] = 6,
                ["seats"] = new JArray(new JObject { ["slot"] = 0, ["name"] = "Ana", ["token"] = "ficha-0" }),
                ["phase"] = "base",
                ["mode"] = "campaign",
                ["campaign"] = CampaignOf("saves/v24.json"),
                ["resultApplied"] = false,
                ["rng"] = new JArray(1, 2, 3, 4),
                ["missionKind"] = "elimination",
                ["skirmishMap"] = "city",
                ["skirmishVeterans"] = true,
            };
            File.WriteAllText(host.SavePath, save.ToString());
            host.Room = host.Make();
            Assert.Equal("lobby", host.Room.Summary().phase);
            var ana = host.Join("Ana", "ficha-0");
            Assert.Equal($"save.{SaveStore.OutdatedLabel}.json", (string?)ana.Take("room").Last()["setAside"]);
            Assert.Empty(ana.Take("campaign"));
            // A new one is of runs.
            host.Say(ana, new { t = "newCampaign" });
            Assert.Equal(Campaign.RunsCampaignVersion, (int)CampaignSent(ana)["version"]!);
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo") || l.Contains("ilegible"));
        }

        /// <summary>
        /// A campaign mission opened again with the partner not here yet (UI's test of the 0.11.7: the host's soldiers all
        /// down, a bad mission left and «Continuar campaña»): the squad's turn waits for them as for one whose laptop went to
        /// sleep (AwayGraceMs), and both hear how long; past that, it ends as after any drop.
        /// </summary>
        [Theory]
        [InlineData("save")]
        [InlineData("continue")]
        public void AMissionOpenedAgainLeavesThePartnersSoldiersToTheOneHere(string how)
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            host.Say(ana, new { t = "newCampaign" });
            GoOut(host, ana, pepe);
            // Ana's soldiers all down, as the save keeps it.
            host.Say(ana, new { t = "backToMenu" });
            var store = new SaveStore(host.SavePath, null, new EngineCampaignRules());
            var save = store.Load()!;
            foreach (var u in save.State!.Units.Where((u) => u.Team == Teams.Xcom && u.Owner == 0))
            {
                u.Alive = false;
                u.Hp = 0;
            }
            var turn = save.State.Turn;
            Assert.Equal(Teams.Xcom, save.State.ActiveTeam);
            Assert.Contains(save.State.Units, (u) => u.Team == Teams.Xcom && u.Owner == 1 && u.Alive && u.Ap > 0);
            if (how == "save") save.Phase = RoomPhases.Mission;
            store.Save(save);
            host.Room = host.Make();
            ana = host.Join("Ana");
            if (how == "continue") host.Say(ana, new { t = "continueCampaign" });
            Assert.Equal("mission", host.Room.Summary().phase);
            var s = new SaveStore(host.SavePath, null, new EngineCampaignRules()).Load()!.State!;
            Assert.Equal((turn, Teams.Xcom), (s.Turn, s.ActiveTeam));
            // Nobody waited for (Arturo, 7/11: «no esperar nunca»; since the 0.11.8 it was AwayGraceMs): Pepe's soldiers are
            // Ana's at once, and the squad's turn waits for them as for her own (Rules.MaybeEndXcomTurn), however long.
            Assert.Equal(0, s.Command);
            var partner = Players(ana.Take("room").Last()).Single((p) => (int)p["slot"]! == 1);
            Assert.False((bool)partner["connected"]!);
            Assert.Null(partner["graceMs"]);
            host.Advance(Room.AwayGraceMs + 2000);
            s = new SaveStore(host.SavePath, null, new EngineCampaignRules()).Load()!.State!;
            Assert.Equal((turn, Teams.Xcom, 0), (s.Turn, s.ActiveTeam, s.Command));
            Assert.DoesNotContain(ana.Take("events").SelectMany((m) => m["events"]!), (e) => (string?)e["type"] == "turnBegan");
            if (how == "continue")
            {
                // Pepe comes: the turn is still the squad's, and the command goes to Pepe, from Ana with nobody of Ana's own to move
                // (Rules.SyncTurn, AutoPassCommand).
                pepe = host.Join("Pepe");
                host.Advance(2000);
                s = new SaveStore(host.SavePath, null, new EngineCampaignRules()).Load()!.State!;
                Assert.Equal((turn, Teams.Xcom), (s.Turn, s.ActiveTeam));
                Assert.Equal(1, s.Command);
            }
            else
            {
                // Pepe doesn't: it ends when Ana says so.
                host.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
                host.Advance(2000);
                s = new SaveStore(host.SavePath, null, new EngineCampaignRules()).Load()!.State!;
                Assert.True(s.Turn > turn || s.Outcome != null);
            }
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo"));
        }

        private static List<JObject> Players(JObject room) => ((JArray)room["players"]!).Cast<JObject>().ToList();

        [Theory]
        [InlineData(MissionKinds.Recovery, null)]
        [InlineData(MissionKinds.Sabotage, 7)]
        public void AMissionSavedBeforeIsBroughtUpToDate(string kind, int? clock)
        {
            // A mission saved half played in the 0.11.9, with a clock and no movement spent (Unit.moves null): the clock only
            // where there is one now, the sabotage's alarm (Arturo, 5/11 #1), and the whole turn's movement (#14).
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            host.Say(ana, new { t = "newCampaign" });
            GoOut(host, ana, pepe);
            host.Say(ana, new { t = "backToMenu" });
            var store = new SaveStore(host.SavePath, null, new EngineCampaignRules());
            var save = store.Load()!;
            save.State!.Mission.Kind = kind;
            save.State.Mission.TurnsLeft = 7;
            foreach (var u in save.State.Units) u.Moves = null;
            save.Phase = RoomPhases.Mission;
            store.Save(save);
            host.Room = host.Make();
            ana = host.Join("Ana");
            var s = ana.Take("snapshot").Last()["state"]!.ToObject<GameState>(Json.Serializer)!;
            Assert.Equal(clock, s.Mission.TurnsLeft);
            Assert.All(Grid.Combatants(s, Teams.Xcom), (u) => Assert.Equal(Paths.FullMoves(u), Paths.MoveBudget(u)));
        }

        [Fact]
        public void TheRecordingHasWhatItSaysItHas()
        {
            // A new campaign alone and with two, a siege with its posts and one at night, and the saves of version 25.
            var states = Launched.ToDictionary((m) => (string)m["source"]!, (m) => m["state"]!);
            Assert.Equal(7, states.Count);
            bool Partner(JToken s) => s["units"]!.Any((u) => (string?)u["team"] == "xcom" && (int?)u["owner"] == 1);
            Assert.False(Partner(states["states.json#solo-new"]));
            Assert.True(Partner(states["states.json#duo-new"]));
            // The sieges are fought on the base: one with its posts, one at night.
            Assert.All(new[] { "states.json#siege-posts", "states.json#mission-5" }, (n) => Assert.NotNull(states[n]["base"]));
            Assert.True(CampaignOf("states.json#siege-posts")["siegePosts"]!.HasValues);
            var night = CampaignOf("states.json#mission-5").ToObject<CampaignState>(Json.Serializer)!;
            Assert.True(Campaign.ActiveOffer(night)!.Night);
        }
    }
}
