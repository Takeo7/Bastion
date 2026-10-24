using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/appearance.test.ts, test by test: the five of «armour pieces», the one of «the survivor model»
    /// and the six of «the soldier's sex (playtest 2026-10-13)». Its helper `run`, here as a private one.
    /// Where TypeScript tells a piece set to null from one left out (`helmet: null` is no colour), C# can't: null and
    /// missing are the same (README), so those checks are left out (isAppearance's, and the protocol's `torso: null`),
    /// and so is the `extra` field C#'s Appearance has no place for.
    /// What protocol.ts's campaignCommandSchema takes («the protocol takes looks…», «… and the protocol takes it») is asked
    /// of the C# protocol instead: Net/Room.cs checks every order off the wire before the campaign sees it (Wire, below).
    /// `Object.keys` of a look is the order C# writes its fields in, which is always its properties' (normalizeAppearance's),
    /// however the look was made.
    /// </summary>
    public class AppearanceTests
    {
        /// <summary>A look from before armour pieces could be painted.</summary>
        private static Appearance Old()
        {
            return new Appearance { Primary = "#8f2f2f", Secondary = "#c9a13a", Visor = "#7dffb0", Head = HeadIds.Helmet, Build = BuildIds.Slim };
        }

        /// <summary>`{ ...a, … }`: a copy of `a` with what `edit` changes.</summary>
        private static Appearance With(Appearance a, Action<Appearance>? edit = null)
        {
            var copy = new Appearance
            {
                Primary = a.Primary,
                Secondary = a.Secondary,
                Visor = a.Visor,
                Head = a.Head,
                Build = a.Build,
                Sex = a.Sex,
                Helmet = a.Helmet,
                Torso = a.Torso,
                Arms = a.Arms,
                Legs = a.Legs,
                Character = a.Character,
            };
            edit?.Invoke(copy);
            return copy;
        }

        /// <summary>toEqual: the same JSON (Step2Json.Diff: null and missing alike).</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            Assert.True(diff == null, diff);
        }

        /// <summary>`Object.keys(a)`: the fields its JSON has, in the order C# writes them (its properties', which is normalizeAppearance's).</summary>
        private static List<string> Keys(Appearance a)
        {
            return Step2Json.Element(a).EnumerateObject().Select((p) => p.Name).ToList();
        }

        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0)
        {
            var r = Campaign.ExecuteCampaignCommand(c, cmd, new CampaignContext { Slot = slot, Connected = new[] { 0 }, Rng = new Rng(11) });
            if (!r.Ok) throw new InvalidOperationException(r.Error);
            return r;
        }

        private static CustomizeCommand Customize(CampaignSoldier s, string name, Appearance appearance)
        {
            return new CustomizeCommand { Soldier = s.Id, Name = name, Nickname = "", Appearance = appearance };
        }

        /// <summary>The campaign's JSON of a look: as it is stored, and as it goes on the wire.</summary>
        private static string JsonOf(Appearance a) => CampaignJsonTests.Write(a);

        /// <summary>
        /// `campaignCommandSchema.safeParse(cmd).success`, asked of the C# protocol: a room on the base (with the room
        /// tests' FakeRules and FakeCampaignRules) that is sent the order as a player's app would. Net/Room.cs checks it
        /// before the campaign sees it (ValidCampaignCommand): what the check refuses is answered «Mensaje no válido.»,
        /// and what it takes reaches the campaign, which answers it (the fake refuses a customize: «No.»).
        /// </summary>
        private sealed class Wire : IDisposable
        {
            private readonly string dir = Path.Combine(Path.GetTempPath(), "bastion-aspecto-" + Guid.NewGuid().ToString("N"));
            private readonly Room room;
            private readonly MemoryEnd ana;
            private int peers;

            public Wire()
            {
                var campaign = new FakeCampaignRules();
                room = new Room(new SaveStore(Path.Combine(dir, "save.json"), null, campaign), new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(3), null, campaign);
                ana = Join("Ana");
                var pepe = Join("Pepe");
                Say(ana, new JObject { ["t"] = "newCampaign" });
                while (ana.Heard.TryDequeue(out _)) { }
                while (pepe.Heard.TryDequeue(out _)) { }
            }

            private MemoryEnd Join(string name)
            {
                var (player, host) = MemoryEnd.Pair("jugador", $"10.0.0.{++peers}:5000");
                room.Connect(host);
                room.Pump();
                Say(player, new JObject { ["t"] = "hello", ["name"] = name, ["version"] = "1.0.0" });
                return player;
            }

            private void Say(MemoryEnd player, JObject message)
            {
                player.Send(message.ToString());
                room.Pump();
            }

            /// <summary>A customize of «s1» by «Ada» with `appearance`, as the TypeScript test sends it.</summary>
            public bool Takes(JToken? appearance)
            {
                var cmd = new JObject { ["type"] = "customize", ["soldier"] = "s1", ["name"] = "Ada", ["nickname"] = "" };
                if (appearance != null) cmd["appearance"] = appearance;
                Say(ana, new JObject { ["t"] = "ccmd", ["cmd"] = cmd });
                var refused = ana.Take("error").Count;
                var answered = ana.Take("rejected").Count + ana.Take("campaign").Count;
                Assert.Equal(1, refused + answered);
                return refused == 0;
            }

            public bool Takes(Appearance appearance) => Takes(JObject.Parse(JsonOf(appearance)));

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
        }

        // ------------------------------------------------------------ armour pieces

        [Fact(DisplayName = "armour pieces › an old look is still valid and every piece wears what it wore before")]
        public void AnOldLookIsStillValid()
        {
            var old = Old();
            Assert.True(Appearances.IsAppearance(old));
            Assert.True(Appearances.IsAppearance(Appearances.DefaultAppearance(1)));
            foreach (var part in new[] { ArmorParts.Helmet, ArmorParts.Torso, ArmorParts.Arms, ArmorParts.Legs }) Assert.Equal(old.Primary, Appearances.PartColor(old, part));
            // Berets, caps and hoods were painted in the trim; masks and bare heads have nothing to paint.
            foreach (var head in new[] { HeadIds.Hood, HeadIds.Beret, HeadIds.Cap }) Assert.Equal(old.Secondary, Appearances.PartColor(With(old, (a) => a.Head = head), ArmorParts.Helmet));
            Assert.Equal(old.Primary, Appearances.PartColor(With(old, (a) => a.Head = HeadIds.Hood), ArmorParts.Torso));
        }

        [Fact(DisplayName = "armour pieces › pieces take their own colour, and a bad one makes the look invalid")]
        public void PiecesTakeTheirOwnColour()
        {
            var old = Old();
            var look = With(old, (a) =>
            {
                a.Helmet = "#ffffff";
                a.Arms = "#2F6B4F";
            });
            Assert.True(Appearances.IsAppearance(look));
            Assert.Equal("#ffffff", Appearances.PartColor(look, ArmorParts.Helmet));
            Assert.Equal("#2F6B4F", Appearances.PartColor(look, ArmorParts.Arms));
            Assert.Equal(old.Primary, Appearances.PartColor(look, ArmorParts.Legs));
            Assert.Equal("#ffffff", Appearances.PartColor(With(look, (a) => a.Head = HeadIds.Beret), ArmorParts.Helmet));
            Assert.False(Appearances.IsAppearance(With(old, (a) => a.Legs = "red")));
            Assert.False(Appearances.IsAppearance(With(old, (a) => a.Torso = "#12345")));
            // `{ ...old, helmet: null }` is invalid in TypeScript (null is not undefined, nor a colour); in C# a null
            // helmet is one left out (README: null and missing alike), so the look is valid: not checked here.
            Assert.False(Appearances.IsAppearance(With(old, (a) => a.Head = "toString")));
        }

        [Fact(DisplayName = "armour pieces › a stored look keeps only the pieces that differ from what they would follow")]
        public void AStoredLookKeepsOnlyThePiecesThatDiffer()
        {
            var old = Old();
            // (`extra: 'x'`: C#'s Appearance has no place for a field it doesn't know.)
            var look = With(old, (a) =>
            {
                a.Helmet = "#8F2F2F";
                a.Torso = "#ffffff";
                a.Legs = null;
            });
            Same(With(old, (a) => a.Torso = "#ffffff"), Appearances.NormalizeAppearance(look));
            Assert.Equal(new[] { "primary", "secondary", "visor", "head", "build", "torso" }, Keys(Appearances.NormalizeAppearance(look)));
            // A hood follows the trim, so a hood in the armour colour is a choice worth keeping.
            Same(
                With(old, (a) =>
                {
                    a.Head = HeadIds.Hood;
                    a.Helmet = old.Primary;
                }),
                Appearances.NormalizeAppearance(With(old, (a) =>
                {
                    a.Head = HeadIds.Hood;
                    a.Helmet = old.Primary;
                })));
            Same(
                With(old, (a) => a.Head = HeadIds.Hood),
                Appearances.NormalizeAppearance(With(old, (a) =>
                {
                    a.Head = HeadIds.Hood;
                    a.Helmet = old.Secondary;
                })));
        }

        [Fact(DisplayName = "armour pieces › the barracks stores the pieces, and an untouched look is still the default one")]
        public void TheBarracksStoresThePieces()
        {
            var old = Old();
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(4));
            var s = c.Soldiers.First((x) => x.Owner == 0);
            var sex = s.Appearance.Sex;
            var look = With(old, (a) =>
            {
                a.Helmet = "#ffffff";
                a.Torso = "#1d2124";
                a.Arms = "#3fa9ff";
                a.Legs = "#6b7d5a";
            });
            Run(c, Customize(s, s.Name, look));
            Same(With(look, (a) => a.Sex = sex), s.Appearance);

            // Reset to the default with the pieces sent along unchanged (and no sex, like the barracks' button): what is
            // stored compares equal to the default look, so a promotion or a change of owner still restyles the soldier
            // (the JSON checks in campaign.ts).
            var plain = Appearances.DefaultAppearance(0);
            var parts = With(plain, (a) =>
            {
                a.Helmet = Appearances.PartColor(plain, ArmorParts.Helmet);
                a.Torso = Appearances.PartColor(plain, ArmorParts.Torso);
                a.Arms = Appearances.PartColor(plain, ArmorParts.Arms);
                a.Legs = Appearances.PartColor(plain, ArmorParts.Legs);
            });
            Run(c, Customize(s, s.Name, parts));
            Assert.Equal(JsonOf(Appearances.DefaultAppearance(0, null, sex)), JsonOf(s.Appearance));

            Assert.Matches("aspecto", Assert.Throws<InvalidOperationException>(() => Run(c, Customize(s, s.Name, With(plain, (a) => a.Arms = "blue")))).Message);
        }

        [Fact(DisplayName = "armour pieces › the protocol takes looks with or without pieces, and checks their colours")]
        public void TheProtocolTakesLooksWithOrWithoutPieces()
        {
            var old = Old();
            using var wire = new Wire();
            Assert.True(wire.Takes(old));
            Assert.True(wire.Takes(With(old, (a) =>
            {
                a.Helmet = "#ffffff";
                a.Legs = "#000000";
            })));
            Assert.False(wire.Takes(With(old, (a) => a.Arms = "red")));
            // `{ ...old, torso: null }` is refused by campaignCommandSchema (null is not a colour); C# reads a null torso
            // as one left out (README: null and missing alike), so the look is taken: not checked here.
        }

        // ------------------------------------------------------------ the survivor model

        [Fact(DisplayName = "the survivor model › is an optional id the client resolves; the engine only checks its shape and keeps it")]
        public void IsAnOptionalIdTheClientResolves()
        {
            var look = With(Appearances.DefaultAppearance(0), (a) => a.Character = "hunter");
            Assert.True(Appearances.IsAppearance(look));
            Assert.False(Appearances.IsAppearance(With(look, (a) => a.Character = "No vale!")));
            Assert.Equal("hunter", Appearances.NormalizeAppearance(look).Character);
            Assert.DoesNotContain("character", Keys(Appearances.NormalizeAppearance(Appearances.DefaultAppearance(0))));
        }

        // ------------------------------------------------------------ the soldier's sex (playtest 2026-10-13)

        private static string? SexOf(string name)
        {
            return Content.SoldierNames.FirstOrDefault((n) => n.Name == name)?.Sex;
        }

        [Fact(DisplayName = "the soldier's sex (playtest 2026-10-13) › every name has one, and soldiers and recruits get the sex of their name")]
        public void EveryNameHasOne()
        {
            Assert.All(Content.SoldierNames, (n) => Assert.True(n.Sex == Sexes.F || n.Sex == Sexes.M, n.Name));
            Assert.Equal(Content.SoldierNames.Count / 2.0, Content.SoldierNames.Count((n) => n.Sex == Sexes.F));
            Assert.Equal(Content.SoldierNames.Count, new HashSet<string>(Content.SoldierNames.Select((n) => n.Name)).Count);
            Assert.Equal("f", SexOf("Chioma Okafor"));
            Assert.Equal("m", SexOf("Kofi Mensah"));
            var c = Campaign.CreateCampaign(new[] { 0, 1 }, new Rng(4));
            foreach (var s in c.Soldiers) Assert.Equal(SexOf(s.Name), s.Appearance.Sex);
            var solo = Campaign.CreateCampaign(new[] { 0 }, new Rng(4));
            foreach (var s in solo.Soldiers.Skip(2)) s.Alive = false;
            Run(solo, new RecruitCommand());
            var recruit = solo.Soldiers[solo.Soldiers.Count - 1];
            Same(Appearances.DefaultAppearance(0, null, SexOf(recruit.Name), recruit.Id), recruit.Appearance);
            // Written right after the build, like normalizeAppearance stores it, so default looks still compare equal.
            Assert.Equal(new[] { "primary", "secondary", "visor", "head", "build", "sex" }, Keys(recruit.Appearance));
            Assert.Equal(
                new[] { "primary", "secondary", "visor", "head", "build", "sex", "legs", "character" },
                Keys(Appearances.NormalizeAppearance(With(recruit.Appearance, (a) =>
                {
                    a.Character = "hunter";
                    a.Legs = "#ffffff";
                }))));
        }

        [Fact(DisplayName = "the soldier's sex (playtest 2026-10-13) › skirmish squads carry it too")]
        public void SkirmishSquadsCarryItToo()
        {
            var mission = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0, 1 }, Rng = new Rng(1) });
            var squad = mission.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            Assert.True(squad.Count > 0, "no squad");
            foreach (var u in squad)
            {
                Assert.Equal(SexOf(u.Name), u.Appearance?.Sex);
                Same(Appearances.DefaultAppearance(u.Owner!.Value, null, SexOf(u.Name), u.Name), u.Appearance);
            }
        }

        [Fact(DisplayName = "the soldier's sex (playtest 2026-10-13) › people who join by name take theirs: the story's, a relative's, a soldier's")]
        public void PeopleWhoJoinByNameTakeTheirs()
        {
            Assert.Equal("f", Campaign.SexOfPerson("Vera Lindqvist"));
            Assert.Equal("m", Campaign.SexOfPerson("Dr. Femi Adebayo"));
            Assert.Equal("f", Campaign.SexOfPerson("Agente Claire Dubois"));
            Assert.Equal("m", Campaign.SexOfPerson("Kenji Okafor"));
            Assert.Equal("f", Campaign.SexOfPerson("Mariam Rey"));
            Assert.Equal("m", Campaign.SexOfPerson("Hiro Sato"));
            Assert.Null(Campaign.SexOfPerson("Nadie Conocido"));
        }

        [Fact(DisplayName = "the soldier's sex (playtest 2026-10-13) › is part of a valid look, and the protocol takes it")]
        public void IsPartOfAValidLookAndTheProtocolTakesIt()
        {
            var old = Old();
            Assert.True(Appearances.IsAppearance(With(old, (a) => a.Sex = Sexes.F)));
            Assert.True(Appearances.IsAppearance(With(old, (a) => a.Sex = Sexes.M)));
            Assert.False(Appearances.IsAppearance(With(old, (a) => a.Sex = "x")));
            Assert.Null(Appearances.DefaultAppearance(0).Sex);
            Assert.DoesNotContain("sex", Keys(Appearances.DefaultAppearance(0)));
            using var wire = new Wire();
            Assert.True(wire.Takes(With(old, (a) => a.Sex = Sexes.M)));
            Assert.False(wire.Takes(With(old, (a) => a.Sex = "x")));
        }

        [Fact(DisplayName = "the soldier's sex (playtest 2026-10-13) › customizing keeps it unless the look sends another, and a change of owner keeps it too")]
        public void CustomizingKeepsItUnlessTheLookSendsAnother()
        {
            var old = Old();
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(4));
            var s = c.Soldiers.First((x) => x.Owner == 0);
            var sex = s.Appearance.Sex!;
            Run(c, Customize(s, "Otro Nombre", old));
            Assert.Equal(sex, s.Appearance.Sex);
            var other = sex == Sexes.F ? Sexes.M : Sexes.F;
            Run(c, Customize(s, s.Name, With(old, (a) => a.Sex = other)));
            Assert.Equal(other, s.Appearance.Sex);

            // Untouched rookies who pass to a partner arriving late take their colours, and keep their sex.
            var d = Campaign.CreateCampaign(new[] { 0 }, new Rng(3));
            Campaign.AddPlayer(d, 1, new Rng(1));
            var moved = d.Soldiers.Where((x) => x.Owner == 1).ToList();
            Assert.True(moved.Count > 0);
            foreach (var m in moved) Same(Appearances.DefaultAppearance(1, null, Content.SexOfName(m.Name), m.Id), m.Appearance);
        }

        [Fact(DisplayName = "the soldier's sex (playtest 2026-10-13) › every soldier starts with a head and a build of its own, and a campaign of before gets them for the untouched ones (docs/SIDEKICK.md)")]
        public void EverySoldierStartsWithAHeadAndABuildOfItsOwn()
        {
            var c = Campaign.CreateCampaign(new[] { 0, 1 }, new Rng(5));
            Assert.True(c.Soldiers.Select((s) => s.Appearance.Head).Distinct().Count() > 1);
            Assert.True(c.Soldiers.Select((s) => s.Appearance.Build).Distinct().Count() > 1);
            // A campaign of 0.8: everyone in the helmet and the normal build, but one customized.
            var old = JToken.Parse(CampaignJsonTests.Write(c)).ToObject<CampaignState>(CampaignJsonTests.Serializer)!;
            old.Version = 23;
            foreach (var s in old.Soldiers) s.Appearance = Appearances.DefaultAppearance(s.Owner, null, s.Appearance.Sex);
            var custom = old.Soldiers[0];
            custom.Appearance = With(custom.Appearance, (a) =>
            {
                a.Head = HeadIds.Helmet;
                a.Build = BuildIds.Heavy;
            });
            // C#'s migrateCampaign changes the campaign it is given (TypeScript's, a copy): what the customized one was, before.
            var customLook = With(custom.Appearance);
            var m = Campaign.MigrateCampaign(old);
            Same(customLook, m.Soldiers[0].Appearance);
            foreach (var s in m.Soldiers.Skip(1)) Same(Appearances.DefaultAppearance(s.Owner, null, s.Appearance.Sex, s.Id), s.Appearance);
        }
    }
}
