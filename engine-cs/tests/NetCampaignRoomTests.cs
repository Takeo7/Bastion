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
    /// A campaign of its own for the room's tests, on the engine's types, until campaign.ts is in C#: two soldiers each,
    /// with three skill points; an operation chosen from the start; a proposal by «chooseMission» that the partner votes;
    /// «launch» once ready; «unlock» refused on the cell RefusedCell; any other order refused with «No.».
    /// </summary>
    public sealed class FakeCampaignRules : ICampaignRules
    {
        /// <summary>A cell of the board (board.ts BOARD) the fake won't unlock.</summary>
        public const string RefusedCell = "lightTrigger";

        public int Migrated;
        public int Applied;
        public int Pads;
        public GameState? LastMission;
        /// <summary>Bringing a campaign up to date throws, as a bug in migrateCampaign would.</summary>
        public bool MigrationFails;

        public CampaignState MigrateCampaign(CampaignState c)
        {
            if (MigrationFails) throw new KeyNotFoundException("una pieza que ya no existe");
            Migrated++;
            c.Version = Campaign.CampaignVersion;
            return c;
        }

        private static IEnumerable<CampaignSoldier> SoldiersOf(int slot) => new[] { "a", "b" }.Select((x) => new CampaignSoldier
        {
            Id = $"s{slot}{x}",
            Name = $"Soldado {slot}{x}",
            Owner = slot,
            Alive = true,
            Skills = new Skills { [SkillIds.Shooting] = Bars.BarsCost(3) },
        });

        public CampaignState CreateCampaign(IReadOnlyList<int> slots, Rng rng)
        {
            return new CampaignState { Version = Campaign.CampaignVersion, Day = 1, Level = 2, Players = slots.ToList(), Soldiers = slots.SelectMany(SoldiersOf).ToList() };
        }

        public void OpeningMission(CampaignState c)
        {
            c.ActiveMission = "op1";
            c.Squad = c.Soldiers.Select((s) => s.Id).ToList();
        }

        public bool AddPlayer(CampaignState c, int slot, Rng rng)
        {
            if (c.Players.Contains(slot)) return false;
            c.Players.Add(slot);
            c.Soldiers.AddRange(SoldiersOf(slot));
            return true;
        }

        public List<string> DefaultSquad(CampaignState c, IReadOnlyList<int> connected) => c.Soldiers.Where((s) => s.Alive && connected.Contains(s.Owner)).Select((s) => s.Id).ToList();

        public void SyncPads(CampaignState c)
        {
            Pads++;
        }

        public CampaignResult ExecuteCampaignCommand(CampaignState c, CampaignCommand cmd, int slot, IReadOnlyList<int> connected, Rng rng)
        {
            switch (cmd)
            {
                case ChooseMissionCommand m:
                    c.Proposal = new Proposal { By = slot, Action = new ChooseMissionAction { Id = m.Id }, Text = $"Ir a {m.Id}" };
                    return CampaignResult.Success();
                case VoteCommand v:
                    if (c.Proposal == null || c.Proposal.By == slot) return CampaignResult.Failure("No hay nada que votar.");
                    c.Proposal = null;
                    return CampaignResult.Success(v.Accept);
                case LaunchCommand l:
                    return c.ActiveMission == null ? CampaignResult.Failure("Elegid antes una operación.") : CampaignResult.Success(l.Ready);
                case UnlockCommand u:
                    return u.Cell == RefusedCell ? CampaignResult.Failure("Faltan puntos.") : CampaignResult.Success();
                default:
                    return CampaignResult.Failure("No.");
            }
        }

        public (GameState state, string name)? LaunchMission(CampaignState c, Rng rng)
        {
            if (c.ActiveMission == null) return null;
            c.MissionsFlown = (c.MissionsFlown ?? 0) + 1;
            LastMission = new GameState
            {
                MapName = "Granja",
                Turn = 1,
                ActiveTeam = Teams.Xcom,
                Command = 0,
                CampaignMission = new CampaignMission { Id = c.ActiveMission, Name = "Operación 1" },
                Units = { new Unit { Id = "s0a", Team = Teams.Xcom, Owner = 0, Alive = true }, new Unit { Id = "s1a", Team = Teams.Xcom, Owner = 1, Alive = true } },
            };
            return (LastMission, "Operación 1");
        }

        public void ApplyMissionResult(CampaignState c, GameState state, Rng rng)
        {
            Applied++;
            c.Day++;
            c.ActiveMission = null;
        }
    }

    /// <summary>The room with the campaign (Net/Room.cs): the base, its orders and proposals, its missions, and its save.</summary>
    public class NetCampaignRoomTests
    {
        private sealed class Rig : IDisposable
        {
            public readonly FakeClock Clock = new FakeClock();
            public readonly FakeRules Rules = new FakeRules();
            public readonly FakeCampaignRules Campaign = new FakeCampaignRules();
            public readonly string Dir = Path.Combine(Path.GetTempPath(), "bastion-campana-" + Guid.NewGuid().ToString("N"));
            public readonly List<string> Log = new List<string>();
            public Room Room;
            private int peers;

            public Rig()
            {
                Room = Make();
            }

            public string SavePath => Path.Combine(Dir, "save.json");

            public Room Make()
            {
                return new Room(new SaveStore(SavePath, (line) => Log.Add(line), Campaign), Rules, "1.0.0", null, Clock, new Rng(3), null, Campaign);
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
                player.Send(message is string s ? s : JObject.FromObject(message).ToString());
                Room.Pump();
            }

            public void Advance(long ms)
            {
                Clock.Advance(ms);
                Room.Pump();
            }

            /// <summary>Ana and Pepe on the base of a new campaign, with what they heard to get there taken out.</summary>
            public (MemoryEnd ana, MemoryEnd pepe) Base()
            {
                var ana = Join("Ana");
                var pepe = Join("Pepe");
                Say(ana, new { t = "newCampaign" });
                while (ana.Heard.TryDequeue(out _)) { }
                while (pepe.Heard.TryDequeue(out _)) { }
                return (ana, pepe);
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

        private static JObject Last(MemoryEnd player, string kind) => player.Take(kind).Last();

        private static object Ccmd(object cmd) => new { t = "ccmd", cmd };

        [Fact]
        public void ACampaignOpensOnTheBaseForBoth()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana");
            var pepe = rig.Join("Pepe");
            rig.Say(ana, new { t = "newCampaign" });
            foreach (var p in new[] { ana, pepe })
            {
                Assert.Equal("base", (string?)Last(p, "room")["phase"]);
                var state = Last(p, "campaign")["state"]!;
                Assert.Equal(new[] { 0, 1 }, state["players"]!.Select((x) => (int)x));
                Assert.Equal("op1", (string?)state["activeMission"]);
            }
            var saved = JObject.Parse(File.ReadAllText(rig.SavePath));
            Assert.Equal("campaign", (string?)saved["mode"]);
            Assert.Equal(1, (int)saved["campaign"]!["day"]!);
        }

        [Fact]
        public void APartnerArrivingLateGetsSoldiersAndTheSquadIsRedone()
        {
            using var rig = new Rig();
            var ana = rig.Join("Ana");
            rig.Say(ana, new { t = "newCampaign" });
            Assert.Equal(new[] { 0 }, Last(ana, "campaign")["state"]!["players"]!.Select((x) => (int)x));
            var pepe = rig.Join("Pepe");
            var state = Last(pepe, "campaign")["state"]!;
            Assert.Equal(new[] { 0, 1 }, state["players"]!.Select((x) => (int)x));
            Assert.Contains("s1a", state["squad"]!.Select((x) => (string)x!));
            Assert.Equal("base", (string?)Last(pepe, "room")["phase"]);
            Assert.Equal(1, rig.Campaign.Pads);
        }

        [Fact]
        public void OrdersOnTheBaseAreDoneRefusedOrIgnoredAndLogged()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Base();
            rig.Say(ana, Ccmd(new { type = "unlock", soldier = "s0a", cell = FakeCampaignRules.RefusedCell }));
            Assert.Equal("Faltan puntos.", (string?)ana.Take("rejected").Single()["message"]);
            Assert.Empty(pepe.Take("campaign"));
            Assert.Contains(rig.Log, (l) => l.StartsWith("[campaña] Ana (puesto 1): «unlock» rechazada: Faltan puntos. · Soldado 0a (s0a), de 1, vivo, 3 puntos"));
            rig.Say(ana, Ccmd(new { type = "unlock", soldier = "s0a", cell = "steadyHand" }));
            Assert.Single(pepe.Take("campaign"));
            Assert.Contains(rig.Log, (l) => l.StartsWith("[campaña] Ana: steadyHand para s0a · Soldado 0a"));
            // Back in the lobby, an order is let be, and said.
            rig.Say(pepe, new { t = "leaveCampaign" });
            rig.Say(ana, Ccmd(new { type = "launch", ready = true }));
            Assert.Contains("[campaña] Ana: «launch» ignorada (fase lobby)", rig.Log);
            // Not a campaign's order: refused before the room.
            rig.Say(ana, Ccmd(new { type = "teleport" }));
            Assert.Equal("Mensaje no válido.", (string?)ana.Take("error").Single()["message"]);
        }

        [Fact]
        public void OrdersAreCheckedAsCampaignCommandSchemaChecksThem()
        {
            using var rig = new Rig();
            var (ana, _) = rig.Base();
            // One of each kind, as campaignCommandSchema takes them (golden/campaign/commands.json): past the check, each
            // reaches the campaign's rules, which do it or say why not.
            var commands = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "campaign", "commands.json")))["commands"]!;
            Assert.Equal(34, commands.Count);
            foreach (var cmd in commands)
            {
                rig.Say(ana, Ccmd(cmd));
                Assert.True(ana.Take("error").Count == 0, $"{cmd}: refused by the room");
                Assert.True(ana.Take("rejected").Count + ana.Take("campaign").Count == 1, $"{cmd}: no answer from the campaign");
            }
            // What the schema doesn't let through never reaches them.
            var look = new { primary = "#826cbd", secondary = "#a678ff", visor = "#9fe3ff", head = "cap", build = "normal" };
            var bad = new object[]
            {
                new { type = "place", piece = "teleporter", x = 1, y = 1, rot = 0 },
                new { type = "place", piece = "palisade", x = 1, y = 256, rot = 0 },
                new { type = "place", piece = "palisade", x = 1, y = 1, rot = 4 },
                new { type = "place", piece = "palisade", x = 1.5, y = 1, rot = 0 },
                new { type = "fell", tiles = new object[0] },
                new { type = "fell", tiles = new object?[] { null } },
                new { type = "placeLine", piece = "wall", cells = Enumerable.Range(0, 61).Select((i) => new { x = i, y = 0 }), rot = 0 },
                new { type = "mend", piece = "" },
                new { type = "post", soldier = "s0a", at = new { x = -1, y = 0 } },
                new { type = "chooseMission", id = new string('m', 17) },
                new { type = "contact", region = "atlantis" },
                new { type = "answerEvent", option = 2 },
                new { type = "unlock", soldier = "s0a", cell = "x" },
                new { type = "mod", weapon = "w1", mod = "laser", on = true },
                new { type = "buy", item = "railgun" },
                new { type = "equip", soldier = "s0a", slot = "head", item = "firstAid" },
                new { type = "equip", soldier = "s0a", slot = "utility", item = "railgun" },
                new { type = "customize", soldier = "s0a", name = new string('a', Appearances.NameMax * 2 + 1), nickname = "", appearance = look },
                new { type = "customize", soldier = "s0a", name = "Ana", nickname = "", appearance = new { primary = "#12345g", look.secondary, look.visor, look.head, look.build } },
                new { type = "customize", soldier = "s0a", name = "Ana", nickname = "", appearance = new { look.primary, look.secondary, look.visor, look.head, look.build, character = "Hunter" } },
                new { type = "customize", soldier = "s0a", name = "Ana", nickname = "", appearance = new { look.primary, look.secondary, look.visor, look.head, build = "giant" } },
                new { type = "customize", soldier = "s0a", name = "Ana", nickname = "" },
                new { type = "craft", project = "warpDrive" },
                new { type = "assignWeapon", weapon = "w1", soldier = "" },
            };
            foreach (var cmd in bad)
            {
                rig.Say(ana, Ccmd(cmd));
                Assert.True(ana.Take("error").Count == 1, $"{JObject.FromObject(cmd)}: let through");
                Assert.Empty(ana.Take("rejected"));
            }
            // A colour of the look with a line break after it: /$/ in JavaScript ends there, and so here.
            rig.Say(ana, Ccmd(new { type = "customize", soldier = "s0a", name = "Ana", nickname = "", appearance = new { primary = "#826cbd\n", look.secondary, look.visor, look.head, look.build } }));
            Assert.Single(ana.Take("error"));
        }

        [Fact]
        public void ALaunchedMissionIsFoldedIntoTheCampaignOnce()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Base();
            // Not ready yet: the campaign goes on as it was, the mission doesn't.
            rig.Say(ana, Ccmd(new { type = "launch", ready = false }));
            Assert.Equal("base", rig.Room.Summary().phase);
            rig.Say(ana, Ccmd(new { type = "launch", ready = true }));
            foreach (var p in new[] { ana, pepe })
            {
                Assert.Equal("mission", (string?)Last(p, "room")["phase"]);
                Assert.Equal("Granja", (string?)p.Take("snapshot").Single()["state"]!["mapName"]);
            }
            rig.Campaign.LastMission!.Outcome = Outcomes.Victory;
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            rig.Say(pepe, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            Assert.Equal(1, rig.Campaign.Applied);
            Assert.Equal(2, (int)Last(pepe, "campaign")["state"]!["day"]!);
            // Over, the menu isn't the way: back to the base first.
            rig.Say(ana, new { t = "backToMenu" });
            Assert.Empty(ana.Take("room"));
            rig.Say(ana, new { t = "returnToBase" });
            Assert.Equal("base", (string?)Last(pepe, "room")["phase"]);
        }

        [Fact]
        public void AnUnansweredProposalGoesAheadWithThePartnersYes()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Base();
            rig.Say(ana, Ccmd(new { type = "chooseMission", id = "granja" }));
            Assert.Equal(30_000, (long)Last(pepe, "deadline")["ms"]!);
            // The proposal goes to both with its action, told by its `type`.
            var proposal = Last(pepe, "campaign")["state"]!["proposal"]!;
            Assert.Equal(("chooseMission", "granja"), ((string?)proposal["action"]!["type"], (string?)proposal["action"]!["id"]));
            Assert.Equal("proposal", (string?)Last(ana, "deadline")["kind"]);
            rig.Advance(29_999);
            Assert.Equal("base", rig.Room.Summary().phase);
            rig.Advance(1);
            // Pepe's yes: the mission goes.
            Assert.Equal("mission", (string?)Last(ana, "room")["phase"]);
            Assert.Single(pepe.Take("snapshot"));
        }

        [Fact]
        public void APartnerAwayHasFiveMinutesToVote()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Base();
            rig.Say(pepe, new { t = "away", away = true });
            rig.Say(ana, Ccmd(new { type = "chooseMission", id = "granja" }));
            rig.Advance(30_000);
            Assert.Equal(Room.AwayGraceMs - 30_000, (long)Last(ana, "deadline")["ms"]!);
            // Voted no meanwhile: the proposal goes, and so does the wait.
            rig.Say(pepe, Ccmd(new { type = "vote", accept = false }));
            Assert.Null(Last(ana, "deadline")["ms"]);
            rig.Advance(Room.AwayGraceMs);
            Assert.Equal("base", (string?)Last(ana, "room")["phase"]);
        }

        [Fact]
        public void ACampaignMissionLeftHalfPlayedWaitsForContinuar()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Base();
            rig.Say(ana, Ccmd(new { type = "launch", ready = true }));
            rig.Say(ana, new { t = "cmd", cmd = new { type = "endTurn", ready = true } });
            var seq = rig.Campaign.LastMission!.Seq;
            rig.Say(pepe, new { t = "backToMenu" });
            var room = Last(ana, "room");
            Assert.Equal("lobby", (string?)room["phase"]);
            Assert.Equal("Operación 1", (string?)room["savedCampaign"]!["inMission"]);
            Assert.Equal(2, (int)room["savedCampaign"]!["level"]!);
            // A skirmish would lose it: not before it is finished.
            rig.Say(ana, new { t = "start" });
            Assert.Equal("Antes, acabad la misión de la campaña que está a medias: «Continuar campaña».", (string?)ana.Take("error").Single()["message"]);
            ana.Take("snapshot");
            rig.Say(ana, new { t = "continueCampaign" });
            Assert.Equal("mission", (string?)Last(pepe, "room")["phase"]);
            Assert.Equal(seq, (int)Last(ana, "snapshot")["state"]!["seq"]!);
        }

        [Fact]
        public void ANewCampaignKeepsTheOldOneBeside()
        {
            using var rig = new Rig();
            var (ana, _) = rig.Base();
            rig.Say(ana, Ccmd(new { type = "unlock", soldier = "s0a", cell = "steadyHand" }));
            rig.Say(ana, new { t = "leaveCampaign" });
            rig.Say(ana, new { t = "newCampaign" });
            var kept = Path.Combine(rig.Dir, "save.antes-de-campana-nueva.json");
            Assert.True(File.Exists(kept));
            Assert.Equal("campaign", (string?)JObject.Parse(File.ReadAllText(kept))["mode"]);
            Assert.Contains(rig.Log, (l) => l.StartsWith("[save] La campaña anterior queda en"));
        }

        [Fact]
        public void TheCampaignComesBackFromItsSave()
        {
            using var rig = new Rig();
            var (ana, _) = rig.Base();
            var token = (string)JObject.Parse(File.ReadAllText(rig.SavePath))["seats"]![0]!["token"]!;
            rig.Room = rig.Make();
            Assert.Equal("base", rig.Room.Summary().phase);
            var back = rig.Join("Ana", token);
            Assert.Equal(0, (int)back.Take("welcome").Single()["slot"]!);
            Assert.Equal("op1", (string?)Last(back, "campaign")["state"]!["activeMission"]);
        }

        [Fact]
        public void AnOldCampaignIsSetAsideAndANewerOneNotRead()
        {
            using var rig = new Rig();
            Directory.CreateDirectory(rig.Dir);
            var save = new JObject { ["version"] = 6, ["seats"] = new JArray(), ["phase"] = "base", ["mode"] = "campaign", ["campaign"] = new JObject { ["version"] = 15, ["day"] = 40 }, ["rng"] = new JArray(1, 2, 3, 4) };
            File.WriteAllText(rig.SavePath, save.ToString());
            rig.Room = rig.Make();
            Assert.Equal("lobby", rig.Room.Summary().phase);
            Assert.True(File.Exists(Path.Combine(rig.Dir, "save.antes-0.7.json")));
            Assert.Equal("save.antes-0.7.json", (string?)Last(rig.Join("Ana"), "room")["setAside"]);
            // An older version of today's: migrated.
            save["campaign"]!["version"] = 24;
            File.WriteAllText(rig.SavePath, save.ToString());
            rig.Room = rig.Make();
            Assert.Equal(1, rig.Campaign.Migrated);
            Assert.Equal("base", rig.Room.Summary().phase);
            // From a newer Bastión: not read, moved aside as it is.
            save["campaign"]!["version"] = 26;
            File.WriteAllText(rig.SavePath, save.ToString());
            rig.Room = rig.Make();
            Assert.True(File.Exists(Path.Combine(rig.Dir, "save.ilegible.json")));
            Assert.Contains(rig.Log, (l) => l.Contains("es de una versión más nueva de Bastión"));
        }

        [Fact]
        public void TheBaseScreenAndThePieceInHandGoToThePartner()
        {
            using var rig = new Rig();
            var (ana, pepe) = rig.Base();
            rig.Say(ana, new { t = "baseView", view = "barracks" });
            var view = pepe.Take("baseView").Single();
            Assert.Equal((0, "barracks"), ((int)view["slot"]!, (string?)view["view"]));
            Assert.Empty(ana.Take("baseView"));
            rig.Say(ana, new { t = "baseCursor", piece = "wall", x = 4, y = 7, rot = 1 });
            Assert.Equal(7, (int)pepe.Take("baseCursor").Single()["y"]!);
            // Put down: null, passed on as well.
            rig.Say(ana, new { t = "baseCursor", piece = (string?)null, x = 4, y = 7, rot = 1 });
            Assert.Null(pepe.Take("baseCursor").Single()["piece"]);
            rig.Say(ana, new { t = "baseView", view = "teleporter" });
            Assert.Equal("Mensaje no válido.", (string?)ana.Take("error").Single()["message"]);
            rig.Say(ana, new { t = "baseCursor", piece = "muro", x = 4, y = 7, rot = 1 });
            Assert.Equal("Mensaje no válido.", (string?)ana.Take("error").Single()["message"]);
            Assert.Empty(pepe.Take("baseCursor"));
            // Back in, the partner's screen is told again.
            pepe.Close("wifi");
            rig.Room.Pump();
            var again = rig.Join("Pepe");
            Assert.Equal("barracks", (string?)again.Take("baseView").Single()["view"]);
        }

        [Fact]
        public void WithoutTheCampaignsRulesTheRoomPlaysSkirmishesOnly()
        {
            var dir = Path.Combine(Path.GetTempPath(), "bastion-sin-campana-" + Guid.NewGuid().ToString("N"));
            try
            {
                var room = new Room(new SaveStore(Path.Combine(dir, "save.json")), new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(1));
                var (player, host) = MemoryEnd.Pair();
                room.Connect(host);
                player.Send("{\"t\":\"hello\",\"name\":\"Ana\",\"version\":\"1.0.0\"}");
                player.Send("{\"t\":\"newCampaign\"}");
                room.Pump();
                Assert.Equal("lobby", room.Summary().phase);
                Assert.Empty(player.Take("campaign"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
