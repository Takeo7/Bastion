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
            // A campaign of runs (16/11): two soldiers each, with their knives; one each going out, on the motorbike's two seats (N4).
            var c = CampaignSent(pepe);
            Assert.Equal(Campaign.RunsCampaignVersion, (int)c["version"]!);
            Assert.Equal(new[] { 0, 1 }, c["players"]!.Select((x) => (int)x));
            Assert.Equal(2, c["squad"]!.Count());
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
            Assert.Equal((1, 1), (Theirs(host.Saved(), 0), Theirs(host.Saved(), 1)));
            pepe.Close("se va");
            host.Room.Pump();
            Assert.Equal((2, 0), (Theirs(host.Saved(), 0), Theirs(host.Saved(), 1)));
            Assert.Equal(2, CampaignSent(ana)["squad"]!.Count());
            pepe = host.Join("Pepe", token);
            Assert.Equal((1, 1), (Theirs(host.Saved(), 0), Theirs(host.Saved(), 1)));
            // Out, the squad stays as it went.
            host.Say(ana, Ccmd(new { type = "startRun" }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            Assert.NotNull(host.Saved().Run);
        }

        [Fact]
        public void APartnerJoiningARunUnderWayIsHandedHalfTheSquad()
        {
            // Runs.HandOver (16/11): Ana went out alone with two of her four (the motorbike's seats, N4); Pepe, coming, takes
            // one of them till the run ends, and gets two of his own on the base.
            using var host = new Host();
            var ana = host.Join("Ana");
            host.Say(ana, new { t = "newCampaign" });
            host.Say(ana, Ccmd(new { type = "startRun" }));
            var squad = host.Saved().Run!.Squad;
            Assert.Equal(2, squad.Count);
            var pepe = host.Join("Pepe");
            var c = CampaignSent(pepe);
            var hands = ((JObject)c["run"]!["hands"]!).Properties().ToDictionary((p) => p.Name, (p) => (int)p.Value);
            Assert.Single(hands);
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
            Assert.DoesNotContain(ana.Take("events").SelectMany((m) => m["events"]!), (e) => (string?)e["t"] == "turnBegan");
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

        /// <summary>The state of the mission as `player` last heard it whole.</summary>
        private static GameState SnapshotOf(MemoryEnd player) => player.Take("snapshot").Last()["state"]!.ToObject<GameState>(Json.Serializer)!;

        /// <summary>The room opened again on its save (the host closed and continued), both back in by their tokens.</summary>
        private static (MemoryEnd ana, MemoryEnd pepe) Reopen(Host host, string anaToken, string pepeToken)
        {
            host.Room = host.Make();
            return (host.Join("Ana", anaToken), host.Join("Pepe", pepeToken));
        }

        /// <summary>
        /// N9 (Red, 17/11): a survivor (N2) through the room, two players and the motorbike's two seats: a Rescate's, and one
        /// loose in a Saqueo (RunMap.LooseSurvivor). Nobody's till talked to; then the talker's on both screens, the
        /// partner's orders for them turned away; theirs still after the partner comes back and after the host closes and
        /// continues; the partner's to order with the talker gone.
        /// </summary>
        [Theory]
        [InlineData(NodeKinds.Rescue)]
        [InlineData(NodeKinds.Normal)]
        public void TheSurvivorIsTheTalkersOnBothScreensAndStaysSo(string kind)
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            var anaToken = (string)ana.Take("welcome").Single()["token"]!;
            var pepeToken = (string)pepe.Take("welcome").Single()["token"]!;
            host.Say(ana, new { t = "newCampaign" });
            host.Say(ana, Ccmd(new { type = "startRun" }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            Assert.Equal(2, host.Saved().Run!.Squad.Count);
            // The first row's first node made that kind (as RunsHelpers.GoTo) in the save, a Saqueo on a map with a survivor
            // loose (its seed), and the host opens it again.
            var store = new SaveStore(host.SavePath, null, new EngineCampaignRules());
            var save = store.Load()!;
            var n = RunMap.Choices(save.Campaign!.Run!)[0];
            n.Kind = kind;
            n.Modifier = null;
            if (n.Seed == null)
            {
                n.Seed = 4242 + n.Row;
                n.Site = Places.Country;
            }
            if (kind != NodeKinds.Rescue)
            {
                while (!RunMap.LooseSurvivor(n)) n.Seed++;
            }
            store.Save(save);
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            host.Say(ana, new { t = "continueCampaign" });
            host.Say(ana, Ccmd(new { type = "chooseNode", node = n.Id }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            Assert.Equal("mission", host.Room.Summary().phase);
            var s = SnapshotOf(pepe);
            Assert.Equal(kind == NodeKinds.Rescue ? MissionKinds.Rescue : MissionKinds.Scavenge, s.Mission.Kind);
            var id = s.Mission.Survivor!;
            Assert.Equal(kind == NodeKinds.Rescue ? id : null, s.Mission.ObjectiveUnit);
            var vip = s.Units.Single((u) => u.Id == id);
            Assert.True(vip.Captive);
            Assert.Null(vip.Owner);
            Assert.Equal(new[] { 0, 1 }, s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).Select((u) => u.Owner ?? -1).OrderBy((o) => o));
            // Whoever has the command, their soldier beside the survivor (as the save keeps it), and the host opens it again.
            var talker = s.Command ?? 0;
            var partner = 1 - talker;
            Assert.Equal(id, SnapshotOf(ana).Mission.Survivor);
            save = store.Load()!;
            var by = save.State!.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Owner == talker);
            by.Pos = Beside(save.State, save.State.Units.Single((u) => u.Id == id));
            store.Save(save);
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            var players = new[] { ana, pepe };
            foreach (var p in players) p.Take("events");
            host.Say(players[talker], new { t = "cmd", cmd = new { type = "ability", unit = by.Id, ability = AbilityIds.Talk } });
            Assert.Empty(players[talker].Take("rejected"));
            // Both hear it: the survivor is the talker's.
            foreach (var p in players)
            {
                var rescued = p.Take("events").SelectMany((m) => m["events"]!).Single((e) => (string?)e["t"] == "vipRescued");
                Assert.Equal((id, by.Id, talker), ((string?)rescued["unit"], (string?)rescued["by"], (int?)rescued["owner"]));
            }
            // The partner, given the command, can't order them.
            host.Say(players[talker], new { t = "cmd", cmd = new { type = "pass" } });
            var to = Beside(new SaveStore(host.SavePath, null, new EngineCampaignRules()).Load()!.State!, vip);
            host.Say(players[partner], new { t = "cmd", cmd = new { type = "move", unit = id, to } });
            Assert.Equal("Es de la escuadra del otro jugador.", (string?)players[partner].Take("rejected").Single()["message"]);
            // The partner back: the survivor the talker's.
            players[partner].Close("se va");
            host.Room.Pump();
            players[partner] = host.Join(partner == 0 ? "Ana" : "Pepe", partner == 0 ? anaToken : pepeToken);
            var back = SnapshotOf(players[partner]).Units.Single((u) => u.Id == id);
            Assert.Equal((false, (int?)talker), (back.Captive, back.Owner));
            // The host closed and continued: the same.
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            players = new[] { ana, pepe };
            foreach (var p in players)
            {
                var again = SnapshotOf(p).Units.Single((u) => u.Id == id);
                Assert.Equal((false, (int?)talker), (again.Captive, again.Owner));
            }
            // The talker gone, the partner orders them.
            players[talker].Close("se va");
            host.Room.Pump();
            host.Advance(Room.ReconnectGraceMs + 2000);
            players[partner].Take("rejected");
            host.Say(players[partner], new { t = "cmd", cmd = new { type = "move", unit = id, to } });
            Assert.Empty(players[partner].Take("rejected"));
            var moved = new SaveStore(host.SavePath, null, new EngineCampaignRules()).Load()!.State!.Units.Single((u) => u.Id == id);
            Assert.Equal((to.X, to.Y, to.Z), (moved.Pos.X, moved.Pos.Y, moved.Pos.Z));
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo") || l.StartsWith("[sala]"));
        }

        /// <summary>The campaign as `player` last heard it whole.</summary>
        private static CampaignState CampaignOf(MemoryEnd player) => CampaignSent(player).ToObject<CampaignState>(Json.Serializer)!;

        /// <summary>The save's campaign changed by `change` and written as it is, with the version it is given: not brought up to date on the way.</summary>
        private static void Rewrite(Host host, Action<CampaignState> change)
        {
            var store = new SaveStore(host.SavePath, null, new EngineCampaignRules());
            var save = store.Load()!;
            change(save.Campaign!);
            store.Save(save);
        }

        /// <summary>
        /// N9 (Red, 17/11): a save of the 0.13.0 (campaign version 28) with the «squad» upgrade at its 6 seats, opened by the
        /// host (N4, Arturo's cuadro 23): down to 2 seats, one each, and the 150 § it cost in the stash, the same to both;
        /// opened again, nothing paid twice.
        /// </summary>
        [Fact]
        public void ASaveOfTheFirstRunsComesDownToTwoSeatsOnceForBoth()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            var anaToken = (string)ana.Take("welcome").Single()["token"]!;
            var pepeToken = (string)pepe.Take("welcome").Single()["token"]!;
            host.Say(ana, new { t = "newCampaign" });
            var had = 0;
            Rewrite(host, (c) =>
            {
                c.Version = Campaign.FirstRunsVersion;
                (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = 1;
                c.Squad = c.Soldiers.Select((x) => x.Id).ToList();
                had = Runs.StashSupplies(c);
            });
            Assert.Equal(Campaign.FirstRunsVersion, (int)JObject.Parse(File.ReadAllText(host.SavePath))["campaign"]!["version"]!);
            for (var open = 0; open < 2; open++)
            {
                (ana, pepe) = Reopen(host, anaToken, pepeToken);
                host.Say(ana, new { t = "continueCampaign" });
                Assert.Equal("base", host.Room.Summary().phase);
                foreach (var p in new[] { ana, pepe })
                {
                    var c = CampaignOf(p);
                    Assert.Equal((Campaign.RunsCampaignVersion, 0, 2), (c.Version, Runs.Level(c, UpgradeIds.Squad), Runs.SeatsOf(c)));
                    Assert.Equal(had + 150, Runs.StashSupplies(c));
                    Assert.Single(c.Log, (e) => e.Text == Lore.SeatsReset(150));
                    Assert.Equal(new[] { 0, 1 }, c.Squad.Select((id) => c.Soldiers.Single((x) => x.Id == id).Owner).OrderBy((o) => o));
                }
            }
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo") || l.StartsWith("[sala]"));
        }

        /// <summary>
        /// N9 (Red, 17/11): a save of the 0.13.0 with a run under way, its four out on the 4 seats it had for nothing: opened
        /// by the host, the run goes on with the four to its end (Migration.MigrateRuns), and both fight with them.
        /// </summary>
        [Fact]
        public void ARunOfTheFirstRunsUnderWayGoesOnWithItsFour()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            var anaToken = (string)ana.Take("welcome").Single()["token"]!;
            var pepeToken = (string)pepe.Take("welcome").Single()["token"]!;
            host.Say(ana, new { t = "newCampaign" });
            // Out with four, as the 0.13.0 went (the 4 seats of now's first upgrade), and saved as the 0.13.0 had it.
            Rewrite(host, (c) => (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = 1);
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            host.Say(ana, new { t = "continueCampaign" });
            host.Say(ana, Ccmd(new { type = "startRun" }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            var squad = host.Saved().Run!.Squad;
            Assert.Equal(4, squad.Count);
            Rewrite(host, (c) =>
            {
                c.Version = Campaign.FirstRunsVersion;
                c.Upgrades!.Remove(UpgradeIds.Squad);
            });
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            host.Say(ana, new { t = "continueCampaign" });
            var run = CampaignOf(pepe);
            Assert.Equal((Campaign.RunsCampaignVersion, 2), (run.Version, Runs.SeatsOf(run)));
            Assert.Equal(squad, run.Run!.Squad);
            var node = RowOf(CampaignSent(ana), 1).First();
            host.Say(ana, Ccmd(new { type = "chooseNode", node }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            Assert.Equal("mission", host.Room.Summary().phase);
            foreach (var p in new[] { ana, pepe })
            {
                var s = SnapshotOf(p);
                var soldiers = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();
                Assert.Equal(squad.OrderBy((id) => id), soldiers.Select((u) => u.CampaignId!).OrderBy((id) => id));
                Assert.Equal(new[] { 0, 0, 1, 1 }, soldiers.Select((u) => u.Owner ?? -1).OrderBy((o) => o));
            }
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo") || l.StartsWith("[sala]"));
        }

        /// <summary>
        /// N9 (Red, 17/11; R4's c2): Pepe's last soldier falls with the whole squad while he is away, and he comes into the room
        /// after the end, before the base. What he gets is the stage over and the campaign with his recruit, and the report
        /// says who it is (RunReport.Recruits), the line his screen can't make by comparing (BaseController.FreeRecruit).
        /// </summary>
        [Fact]
        public void WhoComesAfterTheSquadFellHearsOfTheirRecruit()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            var anaToken = (string)ana.Take("welcome").Single()["token"]!;
            var pepeToken = (string)pepe.Take("welcome").Single()["token"]!;
            host.Say(ana, new { t = "newCampaign" });
            // Pepe's soldier who stays at the base, gone before: his only one is the one going out.
            Rewrite(host, (c) =>
            {
                foreach (var x in c.Soldiers.Where((x) => x.Owner == 1 && !c.Squad.Contains(x.Id))) x.Alive = false;
            });
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            host.Say(ana, new { t = "continueCampaign" });
            GoOut(host, ana, pepe);
            // Pepe goes; Ana leaves the fight as it is, and the save has the whole squad down.
            pepe.Close("se va");
            host.Room.Pump();
            host.Say(ana, new { t = "backToMenu" });
            var store = new SaveStore(host.SavePath, null, new EngineCampaignRules());
            var save = store.Load()!;
            foreach (var u in save.State!.Units.Where((u) => u.Team == Teams.Xcom))
            {
                u.Alive = false;
                u.Hp = 0;
            }
            save.Phase = RoomPhases.Mission;
            store.Save(save);
            host.Room = host.Make();
            ana = host.Join("Ana", anaToken);
            host.Advance(2000);
            Assert.Equal("mission", host.Room.Summary().phase);
            Assert.Equal((Outcomes.Defeat, EndReasons.Wiped), (store.Load()!.State!.Outcome, store.Load()!.State!.OutcomeReason));
            // Pepe comes now: the stage over, and his recruit in the report and in the log.
            pepe = host.Join("Pepe", pepeToken);
            Assert.Equal(Outcomes.Defeat, SnapshotOf(pepe).Outcome);
            var c = CampaignOf(pepe);
            Assert.Null(c.Run);
            var recruit = c.Soldiers.Single((x) => x.Id == c.LastReport!.Run!.Recruits!.Single());
            Assert.Equal((1, true), (recruit.Owner, recruit.Alive));
            Assert.Contains(c.Log, (e) => e.Text == RunTexts.Recruit(recruit.Name));
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo") || l.StartsWith("[sala]"));
        }

        /// <summary>The mission as the room last saved it.</summary>
        private static GameState SavedState(Host host) => new SaveStore(host.SavePath, null, new EngineCampaignRules()).Load()!.State!;

        /// <summary>Every event of `t` that `player` has heard since last asked.</summary>
        private static List<JToken> Heard(MemoryEnd player, string t) => player.Take("events").SelectMany((m) => m["events"]!).Where((e) => (string?)e["t"] == t).ToList();

        /// <summary>A free tile next to `tile` (orthogonal first), out of La Lata's zone, one can stand on.</summary>
        private static Pos NextToZone(GameState s, Pos tile)
        {
            foreach (var d in Grid.Dirs4.Concat(Grid.Dirs8))
            {
                var p = new Pos { X = tile.X + d.X, Y = tile.Y + d.Y, Z = 0 };
                if (Grid.InBounds(s, p.X, p.Y) && !Abilities.InEvacZone(s, p) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null) return p;
            }
            throw new InvalidOperationException("nowhere next to La Lata's zone");
        }

        /// <summary>
        /// T24 (Red, 18/11; Gameplay's contract, .dev-snapshots/gameplay/t24): getting into La Lata and down again through the
        /// room, two players and the motorbike's two seats. One ends a walk on a tile of its zone: both hear `boarded`, and it
        /// is aboard (off the field, safe) on both screens, after the partner comes back and after the host closes and
        /// continues. Down in the squad's next turn: not the partner's to order; its owner's, to a free tile of the zone, and
        /// both hear `alighted` with that tile. Then both in: La Lata goes by itself, and the fight is won, on both screens.
        /// </summary>
        [Fact]
        public void OneGetsIntoLaLataAndDownAgainTheSameOnBothScreens()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            var anaToken = (string)ana.Take("welcome").Single()["token"]!;
            var pepeToken = (string)pepe.Take("welcome").Single()["token"]!;
            host.Say(ana, new { t = "newCampaign" });
            host.Say(ana, Ccmd(new { type = "startRun" }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            // The first row's first node a Saqueo with no modifier, in the save; the host opens it again, and out.
            var store = new SaveStore(host.SavePath, null, new EngineCampaignRules());
            var save = store.Load()!;
            var n = RunMap.Choices(save.Campaign!.Run!)[0];
            n.Kind = NodeKinds.Normal;
            n.Modifier = null;
            if (n.Seed == null)
            {
                n.Seed = 4242 + n.Row;
                n.Site = Places.Country;
            }
            store.Save(save);
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            host.Say(ana, new { t = "continueCampaign" });
            host.Say(ana, Ccmd(new { type = "chooseNode", node = n.Id }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            Assert.Equal("mission", host.Room.Summary().phase);
            var s = SnapshotOf(pepe);
            Assert.True(Rules.LaLataHere(s));
            Assert.Equal((2, 2), (s.Mission.Seats, s.Mission.EvacZone.Count));
            // Whoever has the command, theirs one step from one tile of the zone; the partner's from the other.
            var mover = s.Command ?? 0;
            var partner = 1 - mover;
            save = store.Load()!;
            var st = save.State!;
            var zone = st.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).ToList();
            var mine = st.Units.Single((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Owner == mover);
            var theirs = st.Units.Single((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Owner == partner);
            mine.Pos = NextToZone(st, zone[0]);
            theirs.Pos = NextToZone(st, zone[1]);
            store.Save(save);
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            var players = new[] { ana, pepe };
            var tokens = new[] { anaToken, pepeToken };
            foreach (var p in players) p.Take("events");
            void Say(int slot, object cmd)
            {
                host.Say(players[slot], new { t = "cmd", cmd });
            }
            // The command to `slot`, if it is the other's.
            void Hold(int slot)
            {
                players[1 - slot].Take("rejected");
                if (SavedState(host).Command == 1 - slot) Say(1 - slot, new { type = "pass" });
                var no = players[1 - slot].Take("rejected").Select((r) => (string?)r["message"]).FirstOrDefault();
                Assert.True(SavedState(host).Command != 1 - slot, $"the command not passed to {slot}: «{no}»");
            }

            // In: both hear it, and it is aboard on both screens; La Lata waits for the partner's.
            Say(mover, new { type = "move", unit = mine.Id, to = zone[0] });
            Assert.Empty(players[mover].Take("rejected"));
            foreach (var p in players) Assert.Equal(mine.Id, (string?)Heard(p, "boarded").Single()["unit"]);
            var aboard = SavedState(host);
            var it = aboard.Units.Single((u) => u.Id == mine.Id);
            Assert.Equal((true, false, true), (it.Aboard, it.Alive, it.Evacuated));
            Assert.Equal(((int?)1, (string?)null, false), (Rules.SeatsFree(aboard), aboard.Outcome, aboard.Mission.EvacDone == true));
            Assert.Null(Grid.UnitAt(aboard, zone[0]));
            // The partner back, and the host closed and continued: aboard on both.
            players[partner].Close("se va");
            host.Room.Pump();
            players[partner] = host.Join(partner == 0 ? "Ana" : "Pepe", tokens[partner]);
            Assert.Equal(true, SnapshotOf(players[partner]).Units.Single((u) => u.Id == mine.Id).Aboard);
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            players = new[] { ana, pepe };
            foreach (var p in players)
            {
                var again = SnapshotOf(p).Units.Single((u) => u.Id == mine.Id);
                Assert.Equal((true, false), (again.Aboard, again.Alive));
            }
            // No action left this turn (in with it): the squad's next one, both ready, the dead's turn played.
            var turn = SavedState(host).Turn;
            foreach (var slot in new[] { mover, partner }) Say(slot, new { type = "endTurn", ready = true });
            for (var i = 0; i < 60 && !(SavedState(host).Turn > turn && SavedState(host).ActiveTeam == Teams.Xcom); i++) host.Advance(1000);
            var next = SavedState(host);
            Assert.Equal((turn + 1, Teams.Xcom, (string?)null), (next.Turn, next.ActiveTeam, next.Outcome));
            Assert.True(next.Units.Single((u) => u.Id == theirs.Id).Alive);
            Assert.True(next.Units.Single((u) => u.Id == mine.Id).Ap >= 1);
            foreach (var p in players) p.Take("events");
            // Down: not the partner's to order; its owner's, to a free tile of the zone.
            var tile = Rules.AlightTiles(next).First();
            Hold(partner);
            players[partner].Take("rejected");
            Say(partner, new { type = "ability", unit = mine.Id, ability = AbilityIds.Alight, tile });
            Assert.Equal("Es de la escuadra del otro jugador.", (string?)players[partner].Take("rejected").Single()["message"]);
            Hold(mover);
            Say(mover, new { type = "ability", unit = mine.Id, ability = AbilityIds.Alight, tile });
            Assert.Empty(players[mover].Take("rejected"));
            foreach (var p in players)
            {
                var down = Heard(p, "alighted").Single();
                Assert.Equal((mine.Id, tile.X, tile.Y), ((string?)down["unit"], (int)down["tile"]!["x"]!, (int)down["tile"]!["y"]!));
            }
            var out_ = SavedState(host).Units.Single((u) => u.Id == mine.Id);
            Assert.Equal((true, false, false, tile.X, tile.Y), (out_.Alive, out_.Aboard == true, out_.Evacuated, out_.Pos.X, out_.Pos.Y));
            // Both in: it in again on the zone's other tile, the partner's on this one; La Lata goes by itself.
            var other = zone.First((z) => z.X != tile.X || z.Y != tile.Y);
            Say(mover, new { type = "move", unit = mine.Id, to = other });
            Assert.Empty(players[mover].Take("rejected"));
            Hold(partner);
            Say(partner, new { type = "move", unit = theirs.Id, to = tile });
            Assert.Empty(players[partner].Take("rejected"));
            host.Advance(1000);
            foreach (var p in players)
            {
                var events = p.Take("events").SelectMany((m) => m["events"]!).ToList();
                Assert.Equal(new[] { mine.Id, theirs.Id }, events.Where((e) => (string?)e["t"] == "boarded").Select((e) => (string?)e["unit"]));
                Assert.Single(events, (e) => (string?)e["t"] == "evacDeparted");
                Assert.Equal(Outcomes.Victory, (string?)events.Single((e) => (string?)e["t"] == "missionEnded")["outcome"]);
            }
            Assert.Equal(Outcomes.Victory, SavedState(host).Outcome);
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo") || l.StartsWith("[sala]"));
        }

        /// <summary>
        /// T24 in an Asedio (Red, 18/11; Producto's decision on Red's finding): one of the squad waiting on a tile of La Lata's
        /// zone as it comes gets in by itself at the turn's start, and both hear it; still aboard after the partner comes
        /// back. The partner's, out, can't ask to leave from out there («Solo desde dentro de La Lata.»); walked onto the tile
        /// left open it gets in, and with everyone in La Lata goes, on both screens.
        /// </summary>
        [Fact]
        public void InAnAsedioWhoWaitsOnItsTileGetsInAsItComesOnBothScreens()
        {
            using var host = new Host();
            var ana = host.Join("Ana");
            var pepe = host.Join("Pepe");
            var anaToken = (string)ana.Take("welcome").Single()["token"]!;
            var pepeToken = (string)pepe.Take("welcome").Single()["token"]!;
            host.Say(ana, new { t = "newCampaign" });
            host.Say(ana, Ccmd(new { type = "startRun" }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            var store = new SaveStore(host.SavePath, null, new EngineCampaignRules());
            var save = store.Load()!;
            var n = RunMap.Choices(save.Campaign!.Run!)[0];
            n.Kind = NodeKinds.Hold;
            n.Modifier = null;
            if (n.Seed == null)
            {
                n.Seed = 4242 + n.Row;
                n.Site = Places.Country;
            }
            store.Save(save);
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            host.Say(ana, new { t = "continueCampaign" });
            host.Say(ana, Ccmd(new { type = "chooseNode", node = n.Id }));
            host.Say(pepe, Ccmd(new { type = "vote", accept = true }));
            Assert.Equal("mission", host.Room.Summary().phase);
            var s = SnapshotOf(pepe);
            var comes = s.Mission.HoldUntil!.Value;
            Assert.False(Rules.LaLataHere(s));
            // The turn before it comes: Ana's on one of its tiles, Pepe's beside the other, the dead near them gone.
            save = store.Load()!;
            var st = save.State!;
            var zone = st.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).ToList();
            Assert.Equal(2, zone.Count);
            var mine = st.Units.Single((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Owner == 0);
            var theirs = st.Units.Single((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Owner == 1);
            mine.Pos = zone[0];
            theirs.Pos = NextToZone(st, zone[1]);
            foreach (var z in st.Units.Where((u) => u.Alive && u.Team == Teams.Alien && Grid.Distance(u.Pos, zone[0]) < 12))
            {
                z.Alive = false;
                z.Hp = 0;
            }
            st.Turn = comes - 1;
            store.Save(save);
            (ana, pepe) = Reopen(host, anaToken, pepeToken);
            var players = new[] { ana, pepe };
            foreach (var p in players) p.Take("events");
            foreach (var slot in new[] { 0, 1 }) host.Say(players[slot], new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            for (var i = 0; i < 60 && !(SavedState(host).Turn >= comes && SavedState(host).ActiveTeam == Teams.Xcom); i++) host.Advance(1000);
            var come = SavedState(host);
            Assert.Equal((comes, Teams.Xcom, (string?)null), (come.Turn, come.ActiveTeam, come.Outcome));
            Assert.True(Rules.LaLataHere(come));
            // In by itself as it came: both hear it; it is aboard, the partner's still out, and La Lata waits.
            foreach (var p in players) Assert.Equal(mine.Id, (string?)Heard(p, "boarded").Single()["unit"]);
            Assert.Equal((true, true), (come.Units.Single((u) => u.Id == mine.Id).Aboard == true, come.Units.Single((u) => u.Id == theirs.Id).Alive));
            Assert.NotEqual(true, come.Mission.EvacDone);
            // The partner back: aboard.
            players[1].Close("se va");
            host.Room.Pump();
            players[1] = host.Join("Pepe", pepeToken);
            Assert.Equal(true, SnapshotOf(players[1]).Units.Single((u) => u.Id == mine.Id).Aboard);
            // The partner's, out: no asking to leave from there; onto the tile left open, in, and La Lata goes.
            if (SavedState(host).Command == 0) host.Say(players[0], new { t = "cmd", cmd = new { type = "pass" } });
            Assert.NotEqual(0, SavedState(host).Command);
            players[1].Take("rejected");
            host.Say(players[1], new { t = "cmd", cmd = new { type = "evac", unit = theirs.Id } });
            Assert.Equal("Solo desde dentro de La Lata.", (string?)players[1].Take("rejected").Single()["message"]);
            var open = Rules.OpenTiles(SavedState(host)).Single();
            host.Say(players[1], new { t = "cmd", cmd = new { type = "move", unit = theirs.Id, to = new Pos { X = open.X, Y = open.Y, Z = 0 } } });
            Assert.Empty(players[1].Take("rejected"));
            host.Advance(1000);
            foreach (var p in players)
            {
                var events = p.Take("events").SelectMany((m) => m["events"]!).ToList();
                Assert.Equal(new[] { theirs.Id }, events.Where((e) => (string?)e["t"] == "boarded").Select((e) => (string?)e["unit"]));
                Assert.Single(events, (e) => (string?)e["t"] == "evacDeparted");
                Assert.Equal(Outcomes.Victory, (string?)events.Single((e) => (string?)e["t"] == "missionEnded")["outcome"]);
            }
            Assert.DoesNotContain(host.Log, (l) => l.Contains("Fallo") || l.StartsWith("[sala]"));
        }

        /// <summary>A free tile beside `who`, on their floor, one can stand on and reach them from (as RunRescueTests'), the nearest the map's first.</summary>
        private static Pos Beside(GameState s, Unit who)
        {
            foreach (var d in Grid.Dirs8)
            {
                var p = new Pos { X = who.Pos.X + d.X, Y = who.Pos.Y + d.Y, Z = who.Pos.Z };
                if (Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null && Looting.ReachesToSearch(s, p, who.Pos)) return p;
            }
            throw new InvalidOperationException("nowhere beside them");
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
